using System.Collections.Generic;

namespace PFound.InputRouter
{
    /// <summary>
    /// One active input inside a multi-input source's tick: which concurrent input it is
    /// (<see cref="Id"/>) plus its payload. A source lists one of these per input that is active
    /// this tick; inputs it omits are treated as released.
    /// </summary>
    public readonly struct KeyedIntentReading<TIntent> where TIntent : struct, IIntent
    {
        public readonly InputId Id;
        public readonly TIntent Payload;

        public KeyedIntentReading(InputId id, TIntent payload)
        {
            Id = id;
            Payload = payload;
        }
    }

    /// <summary>
    /// What a multi-input subscriber receives: which concurrent input fired, its payload, and the
    /// phase for THAT input. Each <see cref="InputId"/> advances through its own four-phase
    /// lifecycle independently, so one finger can be Held while another is Started or Ended.
    /// </summary>
    public readonly struct MultiIntentContext<TIntent> where TIntent : struct, IIntent
    {
        public readonly InputId Id;
        public readonly TIntent Payload;
        public readonly IntentPhase Phase;

        public MultiIntentContext(InputId id, TIntent payload, IntentPhase phase)
        {
            Id = id;
            Payload = payload;
            Phase = phase;
        }
    }

    /// <summary>
    /// The pluggable read side for an intent that can have MANY simultaneous inputs (multi-touch,
    /// multiple players/devices). Each tick the router hands the source a reusable buffer to fill
    /// with one <see cref="KeyedIntentReading{TIntent}"/> per currently-active input. Leave an
    /// input out to release it. Like the single-input source this is the only place a backend is
    /// touched, so the core stays engine-free.
    /// </summary>
    public interface IMultiIntentSource<TIntent> where TIntent : struct, IIntent
    {
        /// <summary>
        /// Clear-and-fill <paramref name="active"/> with the currently-active inputs (the router
        /// passes a reused list; do not cache it). Must not throw for a missing device.
        /// </summary>
        void Read(List<KeyedIntentReading<TIntent>> active);
    }
}
