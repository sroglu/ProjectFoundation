namespace PFound.InputRouter
{
    /// <summary>
    /// Marker for a typed intent payload. An intent is a small value type that names a game
    /// action and carries whatever data that action needs (a move vector, an aim position, a
    /// slot number, ...). Implement it on a <c>struct</c> so intents stay allocation-free.
    /// </summary>
    public interface IIntent
    {
    }

    /// <summary>
    /// The result of polling an <see cref="IIntentSource{TIntent}"/> for one tick: whether the
    /// input is currently active, plus the payload the source produced. When
    /// <see cref="IsActive"/> is <c>false</c> the payload is ignored by the router.
    /// </summary>
    public readonly struct IntentReading<TIntent> where TIntent : struct, IIntent
    {
        /// <summary>True while the source's input is being asserted this tick.</summary>
        public readonly bool IsActive;

        /// <summary>The payload captured while active.</summary>
        public readonly TIntent Payload;

        public IntentReading(bool isActive, TIntent payload)
        {
            IsActive = isActive;
            Payload = payload;
        }

        /// <summary>An active reading carrying <paramref name="payload"/>.</summary>
        public static IntentReading<TIntent> Active(TIntent payload)
        {
            return new IntentReading<TIntent>(true, payload);
        }

        /// <summary>The shared "no input this tick" reading.</summary>
        public static readonly IntentReading<TIntent> Inactive = default;
    }

    /// <summary>
    /// What a subscriber receives when its phase fires: the intent's latest payload together
    /// with the phase that triggered the call. Passing the phase lets one handler serve several
    /// phases if it subscribes more than once.
    /// </summary>
    public readonly struct IntentContext<TIntent> where TIntent : struct, IIntent
    {
        public readonly TIntent Payload;
        public readonly IntentPhase Phase;

        public IntentContext(TIntent payload, IntentPhase phase)
        {
            Payload = payload;
            Phase = phase;
        }
    }
}
