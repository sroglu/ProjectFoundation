using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PFound.RemoteResourceCache.Core
{
    /// <summary>
    /// A generic, tiered resource cache: <b>memory → disk → remote</b>. A request is answered by the fastest
    /// tier that has the key; a miss falls through, and a value fetched from a slower tier populates every faster
    /// tier above it. The remote tier is reached through a pluggable <see cref="IResourceTransport"/>; raw bytes
    /// become a <typeparamref name="T"/> through a caller-supplied <see cref="ResourceDecoder{T}"/>, so the whole
    /// type stays engine-free and unit-testable.
    ///
    /// Layered on top of the tiers: <b>single-flight</b> (concurrent requests for one key share a load),
    /// <b>ref-counting</b> (<see cref="AcquireAsync"/>/<see cref="Release"/> pin a resource in memory),
    /// <b>retry</b> with typed <see cref="ResourceFailure"/> results, selectable eviction on both bounded tiers,
    /// a <b>disposal hook</b> for native resources, batch <see cref="PreloadAsync"/>, a synchronous memory-only
    /// <see cref="TryGet"/>, and <see cref="ClearMemory"/>/<see cref="ClearDiskAsync"/>.
    /// </summary>
    public sealed class ResourceCache<T>
    {
        private readonly IResourceTransport _transport;
        private readonly DiskCache _disk;
        private readonly ResourceDecoder<T> _decode;
        private readonly ResourceSizer<T> _sizeOf;
        private readonly CachePolicy _policy;
        private readonly RetryPolicy _retry;
        private readonly Func<TimeSpan, CancellationToken, Task> _delay;

        private readonly MemoryResourceCache<T> _memory;

        private readonly object _flightGate = new object();
        private readonly Dictionary<string, Task<ResourceResult<T>>> _inFlight =
            new Dictionary<string, Task<ResourceResult<T>>>(StringComparer.Ordinal);

        public ResourceCache(
            IResourceTransport transport,
            DiskCache disk,
            ResourceDecoder<T> decode,
            CachePolicy policy = null,
            RetryPolicy retry = null,
            ResourceSizer<T> sizeOf = null,
            Action<T> disposer = null,
            Func<TimeSpan, CancellationToken, Task> delay = null)
        {
            _transport = transport;
            _disk = disk;
            _decode = decode;
            _policy = policy ?? CachePolicy.Default;
            _retry = retry ?? RetryPolicy.Default;
            _sizeOf = sizeOf ?? (_ => 0L);
            _delay = delay ?? ((span, ct) => Task.Delay(span, ct));

            _memory = new MemoryResourceCache<T>(_policy.MaxMemoryCount, _policy.MaxMemoryBytes, _policy.MemoryEviction, disposer);
        }

        // ---- diagnostics -------------------------------------------------------------------------

        public int MemoryCount => _memory.Count;
        public long MemoryBytes => _memory.Bytes;
        public bool InMemory(string key) => _memory.Contains(key);
        public bool IsPinned(string key) => _memory.IsPinned(key);
        public int DiskCount => _disk.Count;
        public long DiskBytes => _disk.Bytes;

        // ---- public API --------------------------------------------------------------------------

        /// <summary>Synchronous, memory-tier-only peek — instant, never touches disk or network. Records the access on a hit.</summary>
        public bool TryGet(string key, out T value) => _memory.TryGet(key, out value);

        /// <summary>Gets the resource for <paramref name="key"/> from the nearest tier, without pinning it.</summary>
        public Task<ResourceResult<T>> GetAsync(string key, CancellationToken cancellationToken = default) =>
            ResolveAsync(key, pin: false, cancellationToken);

        /// <summary>Like <see cref="GetAsync"/>, but on success pins the resource in memory (ref-count +1). Balance every successful acquire with a <see cref="Release"/>.</summary>
        public Task<ResourceResult<T>> AcquireAsync(string key, CancellationToken cancellationToken = default) =>
            ResolveAsync(key, pin: true, cancellationToken);

        /// <summary>Releases one pin taken by <see cref="AcquireAsync"/>. Reaching zero makes the entry evictable (and disposes it if it was only held past the cap by the pin).</summary>
        public void Release(string key) => _memory.Release(key);

        /// <summary>
        /// Warms memory + disk for a set of keys concurrently, de-duplicated within the batch (and against any
        /// in-flight load). Fire-and-forget: the returned task completes when the batch settles; awaiting it is
        /// optional. Honors <paramref name="cancellationToken"/>.
        /// </summary>
        public Task PreloadAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var tasks = new List<Task>();
            foreach (string key in keys)
                if (seen.Add(key))
                    tasks.Add(GetAsync(key, cancellationToken));
            return Task.WhenAll(tasks);
        }

        /// <summary>Removes one entry from the memory tier (disposing its native value); returns false if it wasn't resident.</summary>
        public bool RemoveFromMemory(string key) => _memory.Remove(key);

        /// <summary>Drops the whole memory tier, releasing every native value through the disposal hook.</summary>
        public void ClearMemory() => _memory.Clear();

        /// <summary>Deletes every disk blob and its metadata. Async — it touches the filesystem.</summary>
        public Task ClearDiskAsync() => Task.Run(() => _disk.Clear());

        /// <summary>Persists the disk metadata index now. Call on shutdown so LFU/TimeBased ordering survives a restart.</summary>
        public void FlushDisk() => _disk.Flush();

        // ---- resolution --------------------------------------------------------------------------

        private async Task<ResourceResult<T>> ResolveAsync(string key, bool pin, CancellationToken cancellationToken)
        {
            if (_memory.TryGet(key, out T cached))
            {
                if (pin) _memory.Acquire(key, cached, _sizeOf(cached));
                return ResourceResult<T>.Ok(cached, CacheTier.Memory);
            }

            Task<ResourceResult<T>> flight;
            lock (_flightGate)
            {
                if (!_inFlight.TryGetValue(key, out flight))
                {
                    flight = LoadFromDiskOrRemoteAsync(key, cancellationToken);
                    _inFlight[key] = flight;
                }
            }

            ResourceResult<T> result = await flight.ConfigureAwait(false);

            if (pin && result.Success)
                _memory.Acquire(key, result.Value, _sizeOf(result.Value));

            return result;
        }

        private async Task<ResourceResult<T>> LoadFromDiskOrRemoteAsync(string key, CancellationToken cancellationToken)
        {
            try
            {
                // Tier 2 — disk (offline-first, TTL-checked inside the disk tier).
                if (_disk.TryRead(key, out byte[] diskBytes))
                {
                    if (!TryDecode(key, diskBytes, out T diskValue, out ResourceResult<T> diskFailure))
                        return diskFailure;
                    PopulateMemory(key, diskValue);
                    return ResourceResult<T>.Ok(diskValue, CacheTier.Disk);
                }

                // Tier 3 — remote, with retry over transient faults.
                Exception lastTransient = null;
                for (int attempt = 1; attempt <= _retry.MaxAttempts; attempt++)
                {
                    byte[] bytes;
                    try
                    {
                        bytes = await _transport.FetchAsync(key, cancellationToken).ConfigureAwait(false);
                    }
                    catch (ResourceNotFoundException nf)
                    {
                        return ResourceResult<T>.Fail(ResourceFailureKind.NotFound, nf.Message, nf);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (NullReferenceException)
                    {
                        // A null transport (or other unconfigured dependency) is a programmer
                        // misconfiguration, not a transient network fault — fail fast, never retry.
                        throw;
                    }
                    catch (Exception transient)
                    {
                        lastTransient = transient;
                        if (attempt < _retry.MaxAttempts)
                            await _delay(_retry.BackoffFor(attempt), cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    if (!TryDecode(key, bytes, out T value, out ResourceResult<T> decodeFailure))
                        return decodeFailure;

                    if (_policy.CanCache(key))
                    {
                        _disk.Write(key, bytes);
                        PopulateMemory(key, value);
                    }
                    return ResourceResult<T>.Ok(value, CacheTier.Remote);
                }

                ResourceFailureKind kind = _retry.MaxAttempts > 1
                    ? ResourceFailureKind.RetryExhausted
                    : ResourceFailureKind.Network;
                return ResourceResult<T>.Fail(kind,
                    "Failed to fetch '" + key + "' after " + _retry.MaxAttempts + " attempt(s).", lastTransient);
            }
            finally
            {
                lock (_flightGate) _inFlight.Remove(key);
            }
        }

        private bool TryDecode(string key, byte[] bytes, out T value, out ResourceResult<T> failure)
        {
            try
            {
                value = _decode(bytes);
                failure = default;
                return true;
            }
            catch (Exception e)
            {
                value = default;
                failure = ResourceResult<T>.Fail(ResourceFailureKind.Decode,
                    "Failed to decode resource for '" + key + "'.", e);
                return false;
            }
        }

        private void PopulateMemory(string key, T value)
        {
            if (_policy.CanCache(key))
                _memory.Insert(key, value, _sizeOf(value));
        }
    }
}
