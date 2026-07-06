using System;

namespace PFound.Utilities.Messaging
{
    /// <summary>
    /// An immediate (synchronous) parameterless event bus. Subscribers run inline the
    /// moment <see cref="Raise"/> / <see cref="RaiseProtected"/> is called, in ascending
    /// order (equal orders keep subscription order). Distinct from PFound.Signaling,
    /// which is payload-free and deferred.
    /// </summary>
    public sealed class EventChannel
    {
        private readonly DispatchList<Action> _listeners = new DispatchList<Action>();

        private readonly struct Invoke : IDispatch<Action>
        {
            public void Fire(Action listener)
            {
                listener();
            }
        }

        /// <summary>Live listener count.</summary>
        public int ListenerCount
        {
            get { return _listeners.Count; }
        }

        /// <summary>
        /// Attaches a listener.
        /// </summary>
        /// <param name="callback">The delegate to run when the event is raised.</param>
        /// <param name="order">Lower runs earlier; ties keep subscription order.</param>
        /// <param name="lifetime">Persistent, or self-detaching after one fire.</param>
        /// <param name="guard">
        /// Optional liveness anchor. When it goes stale (e.g. a destroyed
        /// UnityEngine.Object under the Unity host) the listener is dropped on the next
        /// raise. Defaults to the callback's own target.
        /// </param>
        public Subscription Subscribe(
            Action callback,
            int order = 0,
            SubscriptionLifetime lifetime = SubscriptionLifetime.Persistent,
            object guard = null)
        {
            DispatchList<Action> list = _listeners;
            var slot = list.Attach(callback, order, lifetime == SubscriptionLifetime.FireOnce, guard);
            return new Subscription(() => list.Detach(slot));
        }

        /// <summary>Attaches a listener that detaches itself after firing once.</summary>
        public Subscription SubscribeOnce(Action callback, int order = 0, object guard = null)
        {
            return Subscribe(callback, order, SubscriptionLifetime.FireOnce, guard);
        }

        /// <summary>Detaches the first listener equal to <paramref name="callback"/>.</summary>
        public bool Unsubscribe(Action callback)
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

        /// <summary>Fires all listeners inline; a throwing listener aborts the raise.</summary>
        public void Raise()
        {
            _listeners.Raise(default(Invoke), false);
        }

        /// <summary>
        /// Fires all listeners inline; a throwing listener is reported via
        /// <see cref="MessagingEnvironment.ReportFault"/> and the remaining listeners
        /// still run.
        /// </summary>
        public void RaiseProtected()
        {
            _listeners.Raise(default(Invoke), true);
        }
    }
}
