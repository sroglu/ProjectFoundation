using System;
using System.Collections.Generic;
using UnityEngine;
using PFound.LoopScheduler;

namespace PFound.Render.PostProcess
{
    /// <summary>
    /// Concrete implementation of <see cref="IRenderPostProcess"/>. Routes typed requests to the
    /// matching adapter, ticks all adapters once per frame via <c>PFound.LoopScheduler</c> BeforeRender,
    /// and owns the per-frame deltaTime computation (uses <see cref="Time.unscaledDeltaTime"/>).
    /// </summary>
    public sealed class RenderPostProcessService : IRenderPostProcess, IDisposable
    {
        private readonly RenderPostProcessOptions _options;
        private readonly RenderPostProcessAdapterCore[] _adapters;
        private readonly Dictionary<Type, int> _typeToHandle;
        private readonly Action _tickCallback;
        private GameObject _gameLoopOwner;
        private bool _disposed;

        /// <summary>Constructs the service. Registers a BeforeRender callback with <c>PFound.LoopScheduler</c>.</summary>
        /// <param name="options">Configuration snapshot; throws <see cref="ArgumentNullException"/> if null.</param>
        /// <param name="adapters">Pre-built adapter instances; throws <see cref="ArgumentNullException"/> if null.</param>
        public RenderPostProcessService(RenderPostProcessOptions options, IEnumerable<IRenderPostProcessAdapter> adapters)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (adapters == null) throw new ArgumentNullException(nameof(adapters));

            _options = options;

            var list = new List<RenderPostProcessAdapterCore>(8);
            _typeToHandle = new Dictionary<Type, int>();
            int handle = 0;
            foreach (var raw in adapters)
            {
                if (raw == null) throw new ArgumentNullException(nameof(adapters), "Adapter list contains a null entry.");
                if (raw is not RenderPostProcessAdapterCore core)
                {
                    throw new InvalidOperationException(
                        $"Adapter '{raw.GetType().Name}' must inherit from RenderPostProcessAdapterBase<TRequest>. " +
                        "Custom adapters that implement IRenderPostProcessAdapter<TRequest> directly are not supported by Phase 6.");
                }
                if (_typeToHandle.ContainsKey(core.RequestType))
                {
                    throw new InvalidOperationException($"Duplicate adapter registration for request type {core.RequestType.Name}.");
                }
                _typeToHandle[core.RequestType] = handle++;
                list.Add(core);
            }
            _adapters = list.ToArray();

            foreach (var a in _adapters) a.Initialize(this);

            // GameLoop owner — non-MonoBehaviour service needs a hidden owner GameObject (memory pattern).
            _gameLoopOwner = new GameObject("[RenderPostProcessService.Owner]") { hideFlags = HideFlags.HideAndDontSave };
            if (Application.isPlaying)
            {
                // DontDestroyOnLoad is Play-mode only; in EditMode the owner is transient within
                // a single test invocation, so the survive-scene-load guarantee is moot.
                UnityEngine.Object.DontDestroyOnLoad(_gameLoopOwner);
            }

            _tickCallback = Tick;
            PFound.LoopScheduler.LoopScheduler.RegisterBeforeRenderLoop(_tickCallback, _gameLoopOwner);
        }

        /// <inheritdoc />
        public IRenderPostProcessTicket Request<TRequest>(TRequest request, int priority = 0) where TRequest : struct
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RenderPostProcessService));
            if (!_typeToHandle.TryGetValue(typeof(TRequest), out var handle))
            {
                throw new InvalidOperationException($"No adapter registered for request type {typeof(TRequest).Name}.");
            }
            if (_adapters[handle] is not RenderPostProcessAdapterBase<TRequest> typedAdapter)
            {
                throw new InvalidOperationException($"Adapter for {typeof(TRequest).Name} is not derived from RenderPostProcessAdapterBase<TRequest>.");
            }
            if (!typedAdapter.TryAllocateSlot(in request, priority, out int slotId, out int generation, out _))
            {
                // Cap exceeded; return an already-released ticket so caller's Release call is a no-op.
                return new RenderPostProcessTicket(null, handle, -1, -1);
            }
            return new RenderPostProcessTicket(this, handle, slotId, generation);
        }

        /// <summary>Called by <see cref="RenderPostProcessTicket"/> on Release.</summary>
        internal void ReleaseInternal(int handle, int slotId, int generation)
        {
            if (_disposed) return;
            if (handle < 0 || handle >= _adapters.Length) return;
            _adapters[handle].ReleaseSlotByHandle(slotId, generation);
        }

        /// <summary>Drives all adapters once per frame. Zero-alloc on steady state.</summary>
        public void Tick()
        {
            if (_disposed) return;
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < _adapters.Length; i++)
            {
                _adapters[i].TickFrame(dt);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            PFound.LoopScheduler.LoopScheduler.DeregisterBeforeRenderLoop(_tickCallback);
            if (_gameLoopOwner != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_gameLoopOwner);
                else UnityEngine.Object.DestroyImmediate(_gameLoopOwner);
                _gameLoopOwner = null;
            }
            for (int i = 0; i < _adapters.Length; i++) _adapters[i].Dispose();
        }
    }
}
