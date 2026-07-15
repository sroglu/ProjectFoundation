using System;
using System.Collections.Generic;

namespace PFound.InputRouter
{
    /// <summary>
    /// The channel for a multi-input intent. Unlike <see cref="IntentChannel{TIntent}"/> (one edge
    /// bit + one payload), this keeps per-<see cref="InputId"/> edge history, so every concurrent
    /// input runs its own four-phase lifecycle: a source that reports fingers {1,2} this tick after
    /// {1} last tick yields Held for 1 and Started for 2; drop 1 next tick and it yields Ended for 1.
    /// </summary>
    internal sealed class MultiIntentChannel<TIntent> : IIntentChannel where TIntent : struct, IIntent
    {
        private static readonly int PhaseCount = 4;

        private readonly List<Action<MultiIntentContext<TIntent>>>[] _subscribers;
        private IMultiIntentSource<TIntent> _source;
        private readonly List<KeyedIntentReading<TIntent>> _buffer = new List<KeyedIntentReading<TIntent>>();
        private readonly Dictionary<InputId, TIntent> _current = new Dictionary<InputId, TIntent>();
        private readonly Dictionary<InputId, TIntent> _previous = new Dictionary<InputId, TIntent>();

        public IntentGroup Group { get; }

        public MultiIntentChannel(IMultiIntentSource<TIntent> source, IntentGroup group)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            Group = group;
            _subscribers = new List<Action<MultiIntentContext<TIntent>>>[PhaseCount];
            for (int i = 0; i < PhaseCount; i++)
            {
                _subscribers[i] = new List<Action<MultiIntentContext<TIntent>>>();
            }
        }

        /// <summary>Hot-swap the read side while keeping subscribers and per-input phase history.</summary>
        public void SwapSource(IMultiIntentSource<TIntent> source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public IDisposable Subscribe(IntentPhase phase, Action<MultiIntentContext<TIntent>> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            List<Action<MultiIntentContext<TIntent>>> list = _subscribers[(int)phase];
            list.Add(handler);
            return new Subscription(list, handler);
        }

        public void Advance(bool live)
        {
            _current.Clear();
            if (live)
            {
                _buffer.Clear();
                _source.Read(_buffer);
                for (int i = 0; i < _buffer.Count; i++)
                {
                    KeyedIntentReading<TIntent> reading = _buffer[i];
                    _current[reading.Id] = reading.Payload; // last write wins on a duplicate id
                }
            }

            // Started / Held for every currently-active input.
            foreach (KeyValuePair<InputId, TIntent> kv in _current)
            {
                IntentPhase phase = _previous.ContainsKey(kv.Key) ? IntentPhase.Held : IntentPhase.Started;
                Dispatch(kv.Key, kv.Value, phase);
            }

            // Ended for inputs that were active last tick and are gone now.
            foreach (KeyValuePair<InputId, TIntent> kv in _previous)
            {
                if (!_current.ContainsKey(kv.Key))
                {
                    Dispatch(kv.Key, kv.Value, IntentPhase.Ended);
                }
            }

            // Roll current → previous for next tick's edge detection.
            _previous.Clear();
            foreach (KeyValuePair<InputId, TIntent> kv in _current)
            {
                _previous[kv.Key] = kv.Value;
            }
        }

        public void ResetState()
        {
            _current.Clear();
            _previous.Clear();
            _buffer.Clear();
        }

        private void Dispatch(InputId id, TIntent payload, IntentPhase phase)
        {
            List<Action<MultiIntentContext<TIntent>>> list = _subscribers[(int)phase];
            int count = list.Count;
            if (count == 0)
            {
                return;
            }

            MultiIntentContext<TIntent> context = new MultiIntentContext<TIntent>(id, payload, phase);
            for (int i = 0; i < count && i < list.Count; i++)
            {
                list[i](context);
            }
        }

        private sealed class Subscription : IDisposable
        {
            private List<Action<MultiIntentContext<TIntent>>> _list;
            private Action<MultiIntentContext<TIntent>> _handler;

            public Subscription(List<Action<MultiIntentContext<TIntent>>> list, Action<MultiIntentContext<TIntent>> handler)
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
