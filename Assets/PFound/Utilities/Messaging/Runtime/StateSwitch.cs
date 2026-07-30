using System;

namespace PFound.Utilities.Messaging
{
    /// <summary>
    /// A stateful two-position broadcaster. It remembers whether it is on or off and
    /// notifies paired listeners on every edge: the on-callback when it flips on, the
    /// off-callback when it flips off.
    ///
    /// Late-join fast-track: subscribing while the switch is already on immediately runs
    /// that listener's on-callback, so a subscriber can rely on being in sync with the
    /// current state without waiting for the next flip.
    ///
    /// Built on <see cref="EventChannel"/> so it inherits ordered, re-entrancy-safe,
    /// guard-aware dispatch.
    /// </summary>
    public sealed class StateSwitch
    {
        private readonly EventChannel _onEnable = new EventChannel();
        private readonly EventChannel _onDisable = new EventChannel();
        private bool _on;

        public StateSwitch(bool startOn = false)
        {
            _on = startOn;
        }

        /// <summary>Current position.</summary>
        public bool IsOn
        {
            get { return _on; }
        }

        /// <summary>Total paired listeners (counted on the enable side).</summary>
        public int ListenerCount
        {
            get { return _onEnable.ListenerCount; }
        }

        /// <summary>
        /// Attaches a paired listener. <paramref name="whenEnabled"/> runs on the on-edge
        /// (and immediately if the switch is already on); <paramref name="whenDisabled"/>
        /// runs on the off-edge. Disposing the returned handle detaches both sides.
        /// </summary>
        public Subscription Subscribe(
            Action whenEnabled,
            Action whenDisabled,
            int order = 0,
            object guard = null)
        {
            if (whenEnabled == null)
            {
                throw new ArgumentNullException("whenEnabled");
            }
            if (whenDisabled == null)
            {
                throw new ArgumentNullException("whenDisabled");
            }

            Subscription onHandle = _onEnable.Subscribe(whenEnabled, order, SubscriptionLifetime.Persistent, guard);
            Subscription offHandle = _onDisable.Subscribe(whenDisabled, order, SubscriptionLifetime.Persistent, guard);

            if (_on && !MessagingEnvironment.IsStale(guard ?? whenEnabled.Target))
            {
                // Late-join fast-track: sync the newcomer to the live on-state.
                whenEnabled();
            }

            return new Subscription(() =>
            {
                onHandle.Dispose();
                offHandle.Dispose();
            });
        }

        /// <summary>Detaches every paired listener anchored to or targeting <paramref name="owner"/>.</summary>
        public int UnsubscribeAll(object owner)
        {
            _onEnable.UnsubscribeAll(owner);
            return _onDisable.UnsubscribeAll(owner);
        }

        /// <summary>Removes all paired listeners.</summary>
        public void Clear()
        {
            _onEnable.Clear();
            _onDisable.Clear();
        }

        /// <summary>Flips on. Runs enable-callbacks only on an actual off→on transition.</summary>
        public void TurnOn()
        {
            Set(true);
        }

        /// <summary>Flips off. Runs disable-callbacks only on an actual on→off transition.</summary>
        public void TurnOff()
        {
            Set(false);
        }

        /// <summary>Sets the position. Broadcasts only when the position actually changes.</summary>
        public void Set(bool on)
        {
            if (_on == on)
            {
                return;
            }
            _on = on;
            if (on)
            {
                _onEnable.RaiseProtected();
            }
            else
            {
                _onDisable.RaiseProtected();
            }
        }

        /// <summary>Flips to the opposite position.</summary>
        public void Toggle()
        {
            Set(!_on);
        }
    }
}
