using System;
using System.Diagnostics;

namespace PFound.EpochClock
{
    /// <summary>
    /// Unix-epoch game clock: an advancing <see cref="Now"/> plus duration/progress, day/period
    /// boundaries and time comparisons built on <see cref="Timestamp"/>/<see cref="TimestampDelta"/>.
    /// <see cref="TickIndependentNow"/> reports the wall clock for drift diagnosis. Subclasses define
    /// how time advances (<see cref="ClientEpochClock"/> from a stopwatch, <see cref="TestEpochClock"/>
    /// manually). Pure C# — no engine dependency. Main-thread only.
    /// </summary>
    public abstract class EpochClock
    {
        private const double SecondsPerDay = 86400.0;

        /// <summary>Multiplier applied to elapsed real time when advancing <see cref="Now"/>.</summary>
        public float TimeScale { get; set; } = 1f;

        /// <summary>The current (scaled) game time.</summary>
        public abstract Timestamp Now { get; }

        /// <summary>The wall-clock time, independent of ticking/scale — for diagnosing drift.</summary>
        public abstract Timestamp TickIndependentNow { get; }

        /// <summary>Advances <see cref="Now"/> by the real time elapsed since the previous tick × <see cref="TimeScale"/>.</summary>
        public abstract void Tick();

        public bool IsPassed(Timestamp time) => Now >= time;

        public bool IsBetween(Timestamp start, Timestamp end) => Now >= start && Now <= end;

        /// <summary>Time left until <paramref name="end"/>, never negative.</summary>
        public TimestampDelta GetRemainingDurationOrZero(Timestamp end)
        {
            var remaining = end - Now;
            return remaining.IsPositive ? remaining : TimestampDelta.Zero;
        }

        /// <summary>Progress of <see cref="Now"/> from <paramref name="start"/> to <paramref name="end"/>, clamped to [0,1].</summary>
        public float GetCurrentProgressClamped(Timestamp start, Timestamp end)
        {
            double span = end.UnixSeconds - start.UnixSeconds;
            if (span <= 0) return 1f;
            double progress = (Now.UnixSeconds - start.UnixSeconds) / span;
            if (progress < 0) progress = 0;
            else if (progress > 1) progress = 1;
            return (float)progress;
        }

        public Timestamp CalculateEndTime(TimeSpan duration) => Now + TimestampDelta.FromTimeSpan(duration);

        /// <summary>Whole days since the Unix epoch for <paramref name="time"/> (UTC day index).</summary>
        public long GetDayIndex(Timestamp time) => (long)Math.Floor(time.UnixSeconds / SecondsPerDay);

        /// <summary>The UTC midnight at or before <paramref name="time"/>.</summary>
        public Timestamp GetDayStart(Timestamp time) => new Timestamp(Math.Floor(time.UnixSeconds / SecondsPerDay) * SecondsPerDay);

        /// <summary>The next UTC midnight after <see cref="Now"/> (e.g. a daily-reward reset gate).</summary>
        public Timestamp GetNextDayStartTime() => GetDayStart(Now) + TimestampDelta.FromSeconds(SecondsPerDay);
    }

    /// <summary>Clock driven by a <see cref="Stopwatch"/>: <see cref="Tick"/> accrues scaled real time onto a wall-clock base.</summary>
    public sealed class ClientEpochClock : EpochClock
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private readonly double _baseUnixSeconds;
        private double _accumulatedScaled;
        private double _lastStopwatchSeconds;

        public ClientEpochClock() { _baseUnixSeconds = WallNowSeconds(); }

        public override Timestamp Now => new Timestamp(_baseUnixSeconds + _accumulatedScaled);
        public override Timestamp TickIndependentNow => new Timestamp(WallNowSeconds());

        public override void Tick()
        {
            double elapsed = _stopwatch.Elapsed.TotalSeconds;
            double delta = elapsed - _lastStopwatchSeconds;
            _lastStopwatchSeconds = elapsed;
            _accumulatedScaled += delta * TimeScale;
        }

        private static double WallNowSeconds() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
    }

    /// <summary>Deterministic clock for tests: time only moves when you call <see cref="AddDelta"/>/<see cref="AddSeconds"/>.</summary>
    public sealed class TestEpochClock : EpochClock
    {
        private double _nowUnixSeconds;

        public TestEpochClock(Timestamp start = default) { _nowUnixSeconds = start.UnixSeconds; }

        public override Timestamp Now => new Timestamp(_nowUnixSeconds);
        public override Timestamp TickIndependentNow => new Timestamp(_nowUnixSeconds);
        public override void Tick() { }

        public void AddDelta(TimestampDelta delta) => _nowUnixSeconds += delta.Seconds;
        public void AddSeconds(double seconds) => _nowUnixSeconds += seconds;
        public void SetNow(Timestamp time) => _nowUnixSeconds = time.UnixSeconds;
    }
}
