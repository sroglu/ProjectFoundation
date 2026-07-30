using System;
using System.Collections.Generic;
using UnityEngine;

namespace PFound.Render.PostProcess
{
    /// <summary>
    /// Internal non-generic base — the service holds adapters as this type so it can route
    /// `ReleaseSlot` calls without knowing the per-request payload type.
    /// </summary>
    public abstract class RenderPostProcessAdapterCore : IRenderPostProcessAdapter
    {
        /// <inheritdoc />
        public abstract Type RequestType { get; }

        /// <inheritdoc />
        public virtual void Initialize(IRenderPostProcess service) { }

        /// <inheritdoc />
        public abstract void TickFrame(float deltaTime);

        /// <summary>Service-side slot release. Stale generation = no-op (idempotent ticket).</summary>
        internal abstract void ReleaseSlotByHandle(int slotId, int generation);

        /// <inheritdoc />
        public virtual void Dispose() { }
    }

    /// <summary>
    /// Abstract base for built-in + custom adapters. Owns the per-effect stack (tombstoned-slot
    /// list with generation counters), fade-state machine, and snapshot construction. Subclasses
    /// implement <see cref="ResolveAndApply"/> and the per-payload <see cref="GetFadeIn"/> /
    /// <see cref="GetFadeOut"/> hooks.
    /// </summary>
    /// <typeparam name="TRequest">Strongly-typed payload struct.</typeparam>
    public abstract class RenderPostProcessAdapterBase<TRequest> : RenderPostProcessAdapterCore, IRenderPostProcessAdapter<TRequest>
        where TRequest : struct
    {
        private struct Slot
        {
            public bool IsAlive;
            public TRequest Request;
            public int Priority;
            public float FadeInElapsed;
            public float FadeOutElapsed;
            public bool ReleaseRequested;
            public int Generation;
            public long TicketId;
        }

        private Slot[] _slots;
        private int _highWater;
        private readonly Stack<int> _freeSlots = new Stack<int>();
        private readonly List<ActiveRequest<TRequest>> _snapshot = new List<ActiveRequest<TRequest>>(8);
        private readonly int _maxSlots;
        private bool _warnedCapExceeded;
        private long _nextTicketId;

        /// <summary>Emit a Debug.LogWarning when the underlying VolumeComponent is missing (one-shot per session).</summary>
        protected bool WarnOnMissingVolumeComponent { get; }

        /// <inheritdoc />
        public override Type RequestType => typeof(TRequest);

        /// <inheritdoc />
        public PostProcessBlendPolicy Policy { get; protected set; }

        /// <summary>Constructs the base with the declared blend policy + cap.</summary>
        protected RenderPostProcessAdapterBase(PostProcessBlendPolicy policy, int maxSlots = 256, bool warnOnMissingVolumeComponent = true)
        {
            Policy = policy;
            _maxSlots = Mathf.Max(1, maxSlots);
            _slots = new Slot[Mathf.Min(16, _maxSlots)];
            WarnOnMissingVolumeComponent = warnOnMissingVolumeComponent;
        }

        /// <inheritdoc />
        public override void TickFrame(float deltaTime)
        {
            _snapshot.Clear();

            for (int i = 0; i < _highWater; i++)
            {
                if (!_slots[i].IsAlive) continue;

                float fadeWeight;
                if (_slots[i].ReleaseRequested)
                {
                    _slots[i].FadeOutElapsed += deltaTime;
                    float fadeOutDur = GetFadeOut(_slots[i].Request);
                    if (fadeOutDur <= 0f) fadeOutDur = GetFadeIn(_slots[i].Request);
                    if (fadeOutDur <= 0f)
                    {
                        _slots[i].IsAlive = false;
                        _slots[i].Generation++;
                        _slots[i].Request = default;
                        _freeSlots.Push(i);
                        continue;
                    }
                    float t = Mathf.Clamp01(_slots[i].FadeOutElapsed / fadeOutDur);
                    fadeWeight = 1f - t;
                    if (t >= 1f)
                    {
                        _slots[i].IsAlive = false;
                        _slots[i].Generation++;
                        _slots[i].Request = default;
                        _freeSlots.Push(i);
                        continue;
                    }
                }
                else
                {
                    _slots[i].FadeInElapsed += deltaTime;
                    float fadeInDur = GetFadeIn(_slots[i].Request);
                    fadeWeight = fadeInDur <= 0f ? 1f : Mathf.Clamp01(_slots[i].FadeInElapsed / fadeInDur);
                }

                _snapshot.Add(new ActiveRequest<TRequest>(_slots[i].Request, _slots[i].Priority, fadeWeight, _slots[i].TicketId));
            }

            ResolveAndApply(_snapshot, deltaTime);
        }

        /// <inheritdoc />
        public abstract void ResolveAndApply(IReadOnlyList<ActiveRequest<TRequest>> stack, float deltaTime);

        /// <summary>Subclass extracts the per-request FadeIn duration.</summary>
        protected abstract float GetFadeIn(in TRequest request);

        /// <summary>Subclass extracts the per-request FadeOut duration.</summary>
        protected abstract float GetFadeOut(in TRequest request);

        /// <summary>Service-side slot allocation. Returns false when the cap is hit.</summary>
        internal bool TryAllocateSlot(in TRequest request, int priority, out int slotId, out int generation, out long ticketId)
        {
            if (_freeSlots.Count > 0)
            {
                slotId = _freeSlots.Pop();
            }
            else if (_highWater < _maxSlots)
            {
                if (_highWater >= _slots.Length)
                {
                    int newCap = Mathf.Min(_slots.Length * 2, _maxSlots);
                    Array.Resize(ref _slots, newCap);
                }
                slotId = _highWater++;
            }
            else
            {
                if (!_warnedCapExceeded)
                {
                    _warnedCapExceeded = true;
                    Debug.LogWarning($"[Render.PostProcess] {GetType().Name} hit MaxConcurrentRequestsPerEffect cap ({_maxSlots}); dropping new request.");
                }
                slotId = -1;
                generation = -1;
                ticketId = -1L;
                return false;
            }

            ticketId = ++_nextTicketId;
            _slots[slotId].IsAlive = true;
            _slots[slotId].Request = request;
            _slots[slotId].Priority = priority;
            _slots[slotId].FadeInElapsed = 0f;
            _slots[slotId].FadeOutElapsed = 0f;
            _slots[slotId].ReleaseRequested = false;
            _slots[slotId].TicketId = ticketId;
            generation = _slots[slotId].Generation;
            return true;
        }

        /// <summary>Service-side slot release request. Stale generations silently ignored (idempotent ticket).</summary>
        internal override void ReleaseSlotByHandle(int slotId, int generation)
        {
            if (slotId < 0 || slotId >= _highWater) return;
            if (!_slots[slotId].IsAlive) return;
            if (_slots[slotId].Generation != generation) return;
            _slots[slotId].ReleaseRequested = true;
        }

        /// <summary>For tests + service introspection.</summary>
        internal int ActiveSlotCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _highWater; i++) if (_slots[i].IsAlive) n++;
                return n;
            }
        }
    }
}
