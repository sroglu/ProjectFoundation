using System;

namespace PFound.Utilities.Messaging
{
    /// <summary>
    /// An immediate (synchronous) event bus carrying a typed reference payload.
    /// Subscribers run inline on <see cref="Raise"/> / <see cref="RaiseProtected"/> in
    /// ascending order (equal orders keep subscription order). The payload is delivered
    /// through a value-type dispatcher so a raise allocates nothing.
    /// </summary>
    public sealed class EventChannel<TPayload>
    {
        private readonly DispatchList<Action<TPayload>> _listeners = new DispatchList<Action<TPayload>>();

        private readonly struct Invoke : IDispatch<Action<TPayload>>
        {
            private readonly TPayload _payload;

            public Invoke(TPayload payload)
            {
                _payload = payload;
            }

            public void Fire(Action<TPayload> listener)
            {
                listener(_payload);
            }
        }

        /// <summary>Live listener count.</summary>
        public int ListenerCount
        {
            get { return _listeners.Count; }
        }

        /// <summary>
        /// Attaches a listener. See <see cref="EventChannel.Subscribe"/> for parameter
        /// semantics; here the callback receives the raised payload.
        /// </summary>
        public Subscription Subscribe(
            Action<TPayload> callback,
            int order = 0,
            SubscriptionLifetime lifetime = SubscriptionLifetime.Persistent,
            object guard = null)
        {
            DispatchList<Action<TPayload>> list = _listeners;
            var slot = list.Attach(callback, order, lifetime == SubscriptionLifetime.FireOnce, guard);
            return new Subscription(() => list.Detach(slot));
        }

        /// <summary>Attaches a listener that detaches itself after firing once.</summary>
        public Subscription SubscribeOnce(Action<TPayload> callback, int order = 0, object guard = null)
        {
            return Subscribe(callback, order, SubscriptionLifetime.FireOnce, guard);
        }

        /// <summary>Detaches the first listener equal to <paramref name="callback"/>.</summary>
        public bool Unsubscribe(Action<TPayload> callback)
        {
            return _listeners.DetachFirst(callback);
        }

        /// <summary>Detaches every listener anchored to or targeting <paramref name="owner"/>.</summary>
        public int UnsubscribeAll(object owner)
        {
            return _listeners.DetachByGuard(owner);
        }

        /// <summary>Detaches the listener currently executing. Call only from inside a listener.</summary>
        public bool UnsubscribeCurrent()
        {
            return _listeners.DetachFiring();
        }

        /// <summary>Removes every listener.</summary>
        public void Clear()
        {
            _listeners.Clear();
        }

        /// <summary>Eagerly drops listeners whose guard has gone stale.</summary>
        public int RemoveStaleListeners()
        {
            return _listeners.Prune();
        }

        /// <summary>Fires all listeners inline with <paramref name="payload"/>; a throw aborts the raise.</summary>
        public void Raise(TPayload payload)
        {
            _listeners.Raise(new Invoke(payload), false);
        }

        /// <summary>Fires all listeners inline with <paramref name="payload"/>; a throw is reported and the rest still run.</summary>
        public void RaiseProtected(TPayload payload)
        {
            _listeners.Raise(new Invoke(payload), true);
        }
    }
}
