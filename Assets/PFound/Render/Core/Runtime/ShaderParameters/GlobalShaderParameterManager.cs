using System;
using System.Collections.Generic;
using UnityEngine;

namespace PFound.Render.Core.ShaderParameters
{
    /// <summary>
    /// Per-frame publisher of global shader parameters. Providers register with
    /// a priority and are invoked once per frame, in priority order (ascending;
    /// FIFO tiebreaker for equal priorities). Same provider instance cannot be
    /// registered twice — <see cref="Register"/> throws and emits a Console error.
    /// </summary>
    /// <remarks>
    /// Constitution II exception: the static <see cref="Instance"/> singleton is
    /// justified because this manager addresses a process-wide concern that is
    /// performance-critical, main-thread-only, and per-frame. Consumers wanting
    /// strict DI can construct their own instance.
    /// </remarks>
    public sealed class GlobalShaderParameterManager : IDisposable
    {
        private static GlobalShaderParameterManager _instance;
        public static GlobalShaderParameterManager Instance => _instance ??= new GlobalShaderParameterManager();

        private readonly List<PriorityRegistration> _providers = new(8);
        private int _insertionCounter;
        private bool _disposed;

        public int Count => _providers.Count;

        /// <summary>
        /// Register a provider at the given priority. Throws
        /// <see cref="InvalidOperationException"/> if the same instance is
        /// already registered.
        /// </summary>
        public void Register(IGlobalShaderParameterProvider provider, int priority = 0)
        {
            ThrowIfDisposed();
            if (provider == null) throw new ArgumentNullException(nameof(provider));

            for (int i = 0; i < _providers.Count; i++)
            {
                if (ReferenceEquals(_providers[i].Provider, provider))
                {
                    Debug.LogError($"[GlobalShaderParameterManager] Provider '{provider.DebugName}' already registered.");
                    throw new InvalidOperationException($"Provider '{provider.DebugName}' is already registered.");
                }
            }

            var entry = new PriorityRegistration
            {
                Provider = provider,
                Priority = priority,
                InsertionOrder = _insertionCounter++,
                LastPublishedFrame = -1,
            };

            // Sorted insert (ascending Priority, then InsertionOrder).
            int idx = _providers.Count;
            for (int i = 0; i < _providers.Count; i++)
            {
                if (entry.CompareTo(_providers[i]) < 0) { idx = i; break; }
            }
            _providers.Insert(idx, entry);
        }

        /// <summary>Unregister a provider. Returns true if it was registered.</summary>
        public bool Unregister(IGlobalShaderParameterProvider provider)
        {
            if (provider == null || _disposed) return false;
            for (int i = 0; i < _providers.Count; i++)
            {
                if (ReferenceEquals(_providers[i].Provider, provider))
                {
                    _providers.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Invoke all registered providers in priority order. Call once per frame.
        /// Zero managed allocations after warm-up.
        /// </summary>
        public void PublishAll()
        {
            if (_disposed) return;
            int frame = Time.frameCount;
            for (int i = 0; i < _providers.Count; i++)
            {
                _providers[i].Provider.Publish();
                var e = _providers[i];
                e.LastPublishedFrame = frame;
                _providers[i] = e;
            }
        }

        /// <summary>
        /// Editor-only debug enumeration. Copies a snapshot into <paramref name="dest"/>;
        /// may allocate if the list needs to grow.
        /// </summary>
        public void GetSnapshot(IList<ProviderInfo> dest)
        {
            if (dest == null) throw new ArgumentNullException(nameof(dest));
            dest.Clear();
            for (int i = 0; i < _providers.Count; i++)
            {
                var e = _providers[i];
                dest.Add(new ProviderInfo
                {
                    DebugName = e.Provider?.DebugName,
                    Priority = e.Priority,
                    LastPublishedFrame = e.LastPublishedFrame,
                });
            }
        }

        /// <summary>
        /// Remove all registered providers, returning this manager to an empty
        /// registry. Unlike <see cref="Dispose"/> the manager stays usable.
        /// </summary>
        public void Clear()
        {
            ThrowIfDisposed();
            _providers.Clear();
            _insertionCounter = 0;
        }

        /// <summary>
        /// Drop the process-wide <see cref="Instance"/> singleton so the next
        /// access starts from a clean registry. Intended as a test seam — call it
        /// from test SetUp/TearDown to keep runs deterministic regardless of order.
        /// </summary>
        public static void ResetInstance()
        {
            _instance?.Dispose();
            _instance = null;
        }

        public void Dispose()
        {
            _disposed = true;
            _providers.Clear();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GlobalShaderParameterManager));
        }
    }
}