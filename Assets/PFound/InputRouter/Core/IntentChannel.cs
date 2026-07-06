using System;
using System.Collections.Generic;

namespace PFound.InputRouter
{
    /// <summary>
    /// Type-erased face of one registered intent, so the router can hold every channel in a
    /// single ordered map keyed by intent <see cref="Type"/> without being generic itself.
    /// </summary>
    internal interface IIntentChannel
    {
        IntentGroup Group { get; }

        /// <summary>
        /// Advance the phase machine one tick. When <paramref name="live"/> is false the channel
        /// is gated this tick and is fed an inactive sample, so a gate that turns off mid-press
        /// still resolves a clean <see cref="IntentPhase.Ended"/> then <see cref="IntentPhase.Idle"/>.
        /// </summary>
        void Advance(bool live);

        /// <summary>Forget the edge history and last payload (used on unregister).</summary>
        void ResetState();
    }

    /// <summary>
    /// The concrete channel for one intent type. It owns the intent's <see cref="IIntentSource{TIntent}"/>
    /// (swappable in place on replace), the per-phase subscriber lists, and the one bit of edge
    /// history — <c>_wasActive</c> — that turns raw active/inactive samples into the four phases.
    /// </summary>
    internal sealed class IntentChannel<TIntent> : IIntentChannel where TIntent : struct, IIntent
    {
        private static readonly int PhaseCount = 4;

        private readonly List<Action<IntentContext<TIntent>>>[] _subscribers;
        private IIntentSource<TIntent> _source;
        private bool _wasActive;
        private TIntent _payload;

        public IntentGroup Group { get; }

        public IntentChannel(IIntentSource<TIntent> source, IntentGroup group)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            Group = group;
            _subscribers = new List<Action<IntentContext<TIntent>>>[PhaseCount];
            for (int i = 0; i < PhaseCount; i++)
            {
                _subscribers[i] = new List<Action<IntentContext<TIntent>>>();
            }
        }

        /// <summary>Hot-swap the read side while keeping subscribers and phase history.</summary>
        public void SwapSource(IIntentSource<TIntent> source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public IDisposable Subscribe(IntentPhase phase, Action<IntentContext<TIntent>> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            List<Action<IntentContext<TIntent>>> list = _subscribers[(int)phase];
            list.Add(handler);
            return new Subscription(list, handler);
        }

        public void Advance(bool live)
        {
            bool active = false;
            if (live)
            {
                IntentReading<TIntent> reading = _source.Read();
                active = reading.IsActive;
                if (active)
                {
                    _payload = reading.Payload;
                }
            }

            IntentPhase phase = Resolve(_wasActive, active);
            _wasActive = active;

            List<Action<IntentContext<TIntent>>> list = _subscribers[(int)phase];
            int count = list.Count;
            if (count == 0)
            {
                return;
            }

            IntentContext<TIntent> context = new IntentContext<TIntent>(_payload, phase);
            for (int i = 0; i < count && i < list.Count; i++)
            {
                list[i](context);
            }
        }

        public void ResetState()
        {
            _wasActive = false;
            _payload = default;
        }

        private static IntentPhase Resolve(bool was, bool now)
        {
            if (now)
            {
                return was ? IntentPhase.Held : IntentPhase.Started;
            }

            return was ? IntentPhase.Ended : IntentPhase.Idle;
        }

        /// <summary>Handle returned to a subscriber; disposing it detaches exactly that handler.</summary>
        private sealed class Subscription : IDisposable
        {
            private List<Action<IntentContext<TIntent>>> _list;
            private Action<IntentContext<TIntent>> _handler;

            public Subscription(List<Action<IntentContext<TIntent>>> list, Action<IntentContext<TIntent>> handler)
            {
                _list = list;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_list == null)
                {
                    return;
                }

                _list.Remove(_handler);
                _list = null;
                _handler = null;
            }
        }
    }
}
