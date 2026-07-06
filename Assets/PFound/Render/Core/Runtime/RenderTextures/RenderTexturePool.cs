using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace PFound.Render.Core.RenderTextures
{
    /// <summary>
    /// Keyed transient <see cref="UnityEngine.RenderTexture"/> pool with idle-age
    /// eviction and zero-allocation leak detection. Ownership is per-instance —
    /// there is NO global <c>Instance</c> accessor (Constitution II preference).
    /// Construct one per <see cref="UnityEngine.Rendering.Universal.ScriptableRendererFeature"/>
    /// or share via <c>ServiceRegistry</c>.
    /// </summary>
    /// <remarks>
    /// The pool is purely transient: long-lived render targets MUST be managed by
    /// client code via the raw <see cref="UnityEngine.RenderTexture"/> API — they
    /// never enter the pool. Call <see cref="Tick"/> once per frame to advance
    /// eviction + leak sweep. The leak ring buffer is preallocated; the happy
    /// <see cref="Lease"/> / <see cref="Release"/> + leak-write paths produce zero
    /// per-frame managed allocations.
    /// </remarks>
    public sealed class RenderTexturePool : IDisposable
    {
        private readonly RenderTexturePoolOptions _options;
        private readonly Dictionary<RenderTextureKey, Stack<PooledRenderTexture>> _free = new();
        private readonly List<PooledRenderTexture> _all = new();
        private UnsafeRingBuffer<RenderLeakEntry> _leaks;
        private int _nextToken = 1;
        private bool _disposed;

        /// <summary>Toggleable at runtime. Default = <c>Debug.isDebugBuild</c> from options.</summary>
        public bool LogLeaksToConsole { get; set; }

        /// <summary>Monotonic count of leak records dropped due to ring-buffer overflow.</summary>
        public long DroppedLeakCount => _leaks.DroppedCount;

        public RenderTexturePool(RenderTexturePoolOptions options = null)
        {
            _options = options ?? RenderTexturePoolOptions.Default;
            _leaks = new UnsafeRingBuffer<RenderLeakEntry>(_options.LeakRingBufferCapacity, Allocator.Persistent);
            LogLeaksToConsole = _options.LogLeaksToConsole;
        }

        /// <summary>
        /// Lease a transient RT matching <paramref name="key"/>. Reuses pooled
        /// entries when available; allocates a new RT otherwise.
        /// </summary>
        public RenderTextureLease Lease(in RenderTextureKey key)
        {
            ThrowIfDisposed();

            PooledRenderTexture entry;
            if (_free.TryGetValue(key, out var stack) && stack.Count > 0)
            {
                entry = stack.Pop();
            }
            else
            {
                var rt = new RenderTexture(key.Width, key.Height, key.DepthBits, key.Format)
                {
                    antiAliasing = key.MSAA,
                    useDynamicScale = false,
                    name = $"PooledRT[{key}]",
                };
                if (key.HDR) rt.format = RenderTextureFormat.DefaultHDR;
                rt.Create();
                entry = new PooledRenderTexture { RT = rt, Key = key, Token = _nextToken++ };
                _all.Add(entry);
            }

            entry.IsLeased = true;
            entry.LeasedFrame = Time.frameCount;
            entry.LeakReported = false;
            return new RenderTextureLease(entry.RT, key, entry.Token, this);
        }

        /// <summary>Release a lease back to the pool. Safe with <c>default</c> (no-op).</summary>
        public void Release(in RenderTextureLease lease)
        {
            if (_disposed) return;
            if (lease.Token == 0 || lease.Owner != this) return;

            // O(N) over all entries — acceptable for small pool sizes typical in URP usage.
            // For zero-alloc, do not use LINQ.
            for (int i = 0; i < _all.Count; i++)
            {
                var e = _all[i];
                if (e.Token != lease.Token || !e.IsLeased) continue;
                e.IsLeased = false;
                e.LastReleasedFrame = Time.frameCount;
                if (!_free.TryGetValue(e.Key, out var stack))
                {
                    stack = new Stack<PooledRenderTexture>(2);
                    _free[e.Key] = stack;
                }
                stack.Push(e);
                return;
            }
        }

        /// <summary>
        /// Per-frame driver. Performs idle eviction + leak detection sweep. Call
        /// once per frame, typically from a <c>RendererFeature.AddRenderPasses</c>
        /// or a bootstrap <c>Update</c>.
        /// </summary>
        public void Tick(int currentFrame)
        {
            if (_disposed) return;

            // Idle eviction sweep.
            for (int i = _all.Count - 1; i >= 0; i--)
            {
                var e = _all[i];
                if (e.IsLeased) continue;
                if (currentFrame - e.LastReleasedFrame < _options.IdleFrameThreshold) continue;
                if (_free.TryGetValue(e.Key, out var stack))
                {
                    // Remove this entry from the free stack (search; uncommon path).
                    var temp = new Stack<PooledRenderTexture>(stack.Count);
                    while (stack.Count > 0)
                    {
                        var top = stack.Pop();
                        if (!ReferenceEquals(top, e)) temp.Push(top);
                    }
                    while (temp.Count > 0) stack.Push(temp.Pop());
                }
                if (e.RT != null) e.RT.Release();
                if (e.RT != null) UnityEngine.Object.DestroyImmediate(e.RT);
                _all.RemoveAt(i);
            }

            // Leak sweep.
            for (int i = 0; i < _all.Count; i++)
            {
                var e = _all[i];
                if (!e.IsLeased || e.LeakReported) continue;
                int age = currentFrame - e.LeasedFrame;
                if (age < _options.LeakFrameThreshold) continue;

                ReportLeak(e, currentFrame);
                e.LeakReported = true;
            }
        }

        /// <summary>Force-clears all pooled entries. Outstanding leases are not affected.</summary>
        public void ClearAll()
        {
            ThrowIfDisposed();
            for (int i = _all.Count - 1; i >= 0; i--)
            {
                var e = _all[i];
                if (e.IsLeased) continue;
                if (e.RT != null) { e.RT.Release(); UnityEngine.Object.DestroyImmediate(e.RT); }
                _all.RemoveAt(i);
            }
            _free.Clear();
        }

        /// <summary>Try to drain one leak record (FIFO). Returns false if buffer empty.</summary>
        public bool TryReadLeak(out RenderLeakEntry entry) => _leaks.TryRead(out entry);

        /// <summary>Copy up to <c>dest.Length</c> leak records into the destination span.</summary>
        public int GetLeakSnapshot(Span<RenderLeakEntry> dest) => _leaks.Snapshot(dest);

        /// <summary>Backward-compatible overload accepting a <see cref="NativeArray{T}"/>.</summary>
        public int GetLeakSnapshot(NativeArray<RenderLeakEntry> dest)
        {
            // NativeArray<T> exposes AsSpan in modern Collections — fall back via indexer if unavailable.
            int n = dest.Length < _leaks.Count ? dest.Length : _leaks.Count;
            Span<RenderLeakEntry> tmp = stackalloc RenderLeakEntry[n];
            int copied = _leaks.Snapshot(tmp);
            for (int i = 0; i < copied; i++) dest[i] = tmp[i];
            return copied;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Drain outstanding leases.
            int frame = Time.frameCount;
            for (int i = 0; i < _all.Count; i++)
            {
                var e = _all[i];
                if (e.IsLeased && !e.LeakReported)
                {
                    ReportLeak(e, frame);
                    e.LeakReported = true;
                }
                if (e.RT != null) { e.RT.Release(); UnityEngine.Object.DestroyImmediate(e.RT); }
            }
            _all.Clear();
            _free.Clear();
            _leaks.Dispose();
        }

        private void ReportLeak(PooledRenderTexture entry, int reportedFrame)
        {
            FixedString64Bytes keyStr = default;
            // FixedString append from key fields — zero alloc.
            keyStr.Append(entry.Key.Width);
            keyStr.Append('x');
            keyStr.Append(entry.Key.Height);
            keyStr.Append(' ');
            keyStr.Append((int)entry.Key.Format);
            keyStr.Append(' ');
            keyStr.Append((FixedString32Bytes)(entry.Key.HDR ? "HDR" : "LDR"));
            var rec = new RenderLeakEntry(keyStr, entry.LeasedFrame, reportedFrame, Thread.CurrentThread.ManagedThreadId);
            _leaks.Write(rec);
            if (LogLeaksToConsole)
            {
                Debug.LogWarning($"[RenderTexturePool] Leaked lease: key={entry.Key}, leasedFrame={entry.LeasedFrame}, reportedFrame={reportedFrame}, thread={rec.ThreadId}");
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RenderTexturePool));
        }
    }
}
