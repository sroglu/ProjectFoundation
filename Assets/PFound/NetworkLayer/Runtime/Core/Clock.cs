using System.Diagnostics;

namespace PFound.NetworkLayer
{
    /// <summary>
    /// Monotonic millisecond time source. Injected everywhere a deadline is
    /// evaluated so tests can drive time by hand (see <see cref="ManualClock"/>)
    /// instead of sleeping. Kept as an interface rather than a static call so a
    /// single peer's timeouts are testable in isolation.
    /// </summary>
    public interface IClock
    {
        long NowMs { get; }
    }

    /// <summary>Wall-clock-independent monotonic clock backed by a Stopwatch.</summary>
    public sealed class MonotonicClock : IClock
    {
        static readonly Stopwatch Watch = Stopwatch.StartNew();
        public long NowMs => Watch.ElapsedMilliseconds;
    }

    /// <summary>Deterministic clock for tests; advance it explicitly.</summary>
    public sealed class ManualClock : IClock
    {
        long _now;
        public long NowMs => _now;
        public void Advance(long ms) => _now += ms;
        public void Set(long ms) => _now = ms;
    }
}
