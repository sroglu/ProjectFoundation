namespace PFound.InputRouter
{
    /// <summary>
    /// The pluggable read side for a single intent type. The caller supplies one of these per
    /// intent; the router polls it once per tick via <see cref="Read"/>. This is the only place
    /// a real input backend is touched, which is why the router and its core assembly stay
    /// engine-free: a legacy <c>Input</c> source and an Input System source are both just
    /// implementations of this interface living in the caller's own assembly.
    /// </summary>
    public interface IIntentSource<TIntent> where TIntent : struct, IIntent
    {
        /// <summary>
        /// Sample the backend for the current tick and report whether the input is active plus
        /// the payload to route. Must not throw for a missing device; return
        /// <see cref="IntentReading{TIntent}.Inactive"/> instead.
        /// </summary>
        IntentReading<TIntent> Read();
    }
}
