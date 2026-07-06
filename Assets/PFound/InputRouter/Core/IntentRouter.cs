using System;

namespace PFound.InputRouter
{
    /// <summary>
    /// Engine-free, backend-agnostic input router. It turns per-intent samples (produced by
    /// caller-supplied <see cref="IIntentSource{TIntent}"/> adapters) into a four-phase lifecycle
    /// and dispatches each phase to its subscribers, in stable registration order, every
    /// <see cref="Tick"/>.
    ///
    /// Gating layers, resolved per tick per channel:
    ///   * <see cref="Enabled"/> — global kill switch; when off nothing is polled or dispatched.
    ///   * <see cref="GameplayEnabled"/> — silences the <see cref="IntentGroup.Gameplay"/> group only.
    ///   * the injected pointer-over-UI predicate — also silences gameplay only, so system
    ///     shortcuts keep working while the pointer is over UI.
    /// A gated channel is fed an inactive sample rather than skipped, so held inputs release
    /// cleanly and re-press as a fresh edge once the gate reopens.
    /// </summary>
    public sealed class IntentRouter
    {
        private readonly InsertionOrderedMap<Type, IIntentChannel> _channels =
            new InsertionOrderedMap<Type, IIntentChannel>();

        private readonly Func<bool> _pointerOverUi;

        /// <param name="pointerOverUi">
        /// Optional predicate returning true while the pointer is over interactive UI. Injected
        /// so the core takes no dependency on any EventSystem. When null, UI never suppresses.
        /// </param>
        public IntentRouter(Func<bool> pointerOverUi = null)
        {
            _pointerOverUi = pointerOverUi;
        }

        /// <summary>Global switch. When false, <see cref="Tick"/> does nothing at all.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Group switch for <see cref="IntentGroup.Gameplay"/> intents.</summary>
        public bool GameplayEnabled { get; set; } = true;

        /// <summary>True if an intent of <typeparamref name="TIntent"/> currently has a source.</summary>
        public bool IsRegistered<TIntent>() where TIntent : struct, IIntent
        {
            return _channels.Contains(typeof(TIntent));
        }

        /// <summary>
        /// Register the one source for <typeparamref name="TIntent"/>. Fail-fast if this intent
        /// already has a source. Registration order is the dispatch order.
        /// </summary>
        public void Register<TIntent>(IIntentSource<TIntent> source, IntentGroup group)
            where TIntent : struct, IIntent
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            Type key = typeof(TIntent);
            if (_channels.Contains(key))
            {
                throw new InvalidOperationException(
                    "Intent '" + key.Name + "' already has a registered source.");
            }

            _channels.Append(key, new IntentChannel<TIntent>(source, group));
        }

        /// <summary>
        /// Register the one MULTI-input source for <typeparamref name="TIntent"/> — an intent that
        /// can have many simultaneous inputs (multi-touch, several players/devices), each advancing
        /// its own phase lifecycle keyed by <see cref="InputId"/>. Fail-fast if this intent already
        /// has a source. An intent is either single (<see cref="Register{TIntent}"/>) or multi, not both.
        /// </summary>
        public void RegisterMulti<TIntent>(IMultiIntentSource<TIntent> source, IntentGroup group)
            where TIntent : struct, IIntent
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            Type key = typeof(TIntent);
            if (_channels.Contains(key))
            {
                throw new InvalidOperationException(
                    "Intent '" + key.Name + "' already has a registered source.");
            }

            _channels.Append(key, new MultiIntentChannel<TIntent>(source, group));
        }

        /// <summary>
        /// Bind <paramref name="handler"/> to (<typeparamref name="TIntent"/>, <paramref name="phase"/>)
        /// for a MULTI-input intent; the handler fires once per active <see cref="InputId"/> that hits
        /// the phase this tick. Dispose the handle to unbind. Fail-fast if the intent is not registered
        /// as a multi-input source.
        /// </summary>
        public IDisposable SubscribeMulti<TIntent>(IntentPhase phase, Action<MultiIntentContext<TIntent>> handler)
            where TIntent : struct, IIntent
        {
            if (!_channels.TryGet(typeof(TIntent), out IIntentChannel channel))
            {
                throw new InvalidOperationException(
                    "Cannot subscribe to intent '" + typeof(TIntent).Name +
                    "': register a multi-input source first.");
            }

            if (!(channel is MultiIntentChannel<TIntent> multi))
            {
                throw new InvalidOperationException(
                    "Intent '" + typeof(TIntent).Name +
                    "' is registered as a single-input source; use Subscribe, not SubscribeMulti.");
            }

            return multi.Subscribe(phase, handler);
        }

        /// <summary>
        /// Hot-swap the source for an already-registered intent, keeping its subscribers and its
        /// dispatch position. Fail-fast if the intent was never registered.
        /// </summary>
        public void Replace<TIntent>(IIntentSource<TIntent> source) where TIntent : struct, IIntent
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (!_channels.TryGet(typeof(TIntent), out IIntentChannel channel))
            {
                throw new InvalidOperationException(
                    "Cannot replace intent '" + typeof(TIntent).Name + "': it has no registered source.");
            }

            ((IntentChannel<TIntent>)channel).SwapSource(source);
        }

        /// <summary>
        /// Remove an intent entirely: drop its subscribers, reset its phase history, and free its
        /// dispatch slot. Fail-fast if it was never registered.
        /// </summary>
        public void Unregister<TIntent>() where TIntent : struct, IIntent
        {
            Type key = typeof(TIntent);
            if (!_channels.TryGet(key, out IIntentChannel channel))
            {
                throw new InvalidOperationException(
                    "Cannot unregister intent '" + key.Name + "': it has no registered source.");
            }

            channel.ResetState();
            _channels.Remove(key);
        }

        /// <summary>
        /// Bind <paramref name="handler"/> to (<typeparamref name="TIntent"/>, <paramref name="phase"/>).
        /// Dispose the returned handle to unbind. Fail-fast if the intent is not registered.
        /// </summary>
        public IDisposable Subscribe<TIntent>(IntentPhase phase, Action<IntentContext<TIntent>> handler)
            where TIntent : struct, IIntent
        {
            if (!_channels.TryGet(typeof(TIntent), out IIntentChannel channel))
            {
                throw new InvalidOperationException(
                    "Cannot subscribe to intent '" + typeof(TIntent).Name +
                    "': register a source first.");
            }

            return ((IntentChannel<TIntent>)channel).Subscribe(phase, handler);
        }

        /// <summary>
        /// Poll every registered adapter once and dispatch the resulting phases. Call once per
        /// update step. Dispatch runs in registration order and is independent of hashing.
        /// </summary>
        public void Tick()
        {
            if (!Enabled)
            {
                return;
            }

            bool uiBlocking = _pointerOverUi != null && _pointerOverUi();
            bool gameplayLive = GameplayEnabled && !uiBlocking;

            int count = _channels.Count;
            for (int i = 0; i < count; i++)
            {
                IIntentChannel channel = _channels.ValueAt(i);
                bool live = channel.Group == IntentGroup.System || gameplayLive;
                channel.Advance(live);
            }
        }
    }
}
