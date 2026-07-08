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

        /// <summary>Tolerance (seconds) beyond which <see cref="Now"/> vs <see cref="TickIndependentNow"/> drift is reported.</summary>
        public const double DriftToleranceSeconds = 0.1;

        /// <summary>Sink for drift-check reports. Host wires this to the engine logger; defaults to stderr.</summary>
        public static Action<string> DriftLogger = message => Console.Error.WriteLine(message);

        private bool _driftReported;
        private TimestampDelta _lastTickDelta = TimestampDelta.Zero;

        /// <summary>Multiplier applied to elapsed real time when advancing <see cref="Now"/>.</summary>
        public float TimeScale { get; set; } = 1f;

        /// <summary>The current (scaled) game time.</summary>
        public abstract Timestamp Now { get; }

        /// <summary>The wall-clock time, independent of ticking/scale — for diagnosing drift.</summary>
        public abstract Timestamp TickIndependentNow { get; }

        /// <summary>Advances <see cref="Now"/> by the real time elapsed since the previous tick × <see cref="TimeScale"/>.</summary>
        public abstract void Tick();

        /// <summary>The amount <see cref="Now"/> moved during the most recent advance (records the last tick's step).</summary>
        public TimestampDelta LastTickDelta => _lastTickDelta;

        /// <summary>Subclasses record how far <see cref="Now"/> jumped so <see cref="LastTickDelta"/> reflects it.</summary>
        protected void SetLastTickDelta(TimestampDelta delta) => _lastTickDelta = delta;

        /// <summary><see cref="Now"/> rounded up to a whole second.</summary>
        public Timestamp NowRoundedUp => Now.CeilToSecond();

        /// <summary><see cref="Now"/> rounded down to a whole second.</summary>
        public Timestamp NowRoundedDown => Now.FloorToSecond();

        /// <summary>How far the compressed in-game day has advanced (see <see cref="Timestamp.ToScaledDayTime"/>).</summary>
        public TimeSpan CurrentHourOfDay => Now.ToScaledDayTime(CalendarConstants.DefaultDayLengthSeconds);

        #region Comparison

        public bool IsPassed(Timestamp time) => Now >= time;

        public bool IsBetween(Timestamp start, Timestamp end) => Now >= start && Now <= end;

        #endregion

        #region Passed / remaining duration

        /// <summary>Signed duration since <paramref name="time"/> (positive once it has passed).</summary>
        public TimestampDelta GetPassedDuration(Timestamp time) => Now - time;

        /// <summary>Duration since <paramref name="time"/>, never negative.</summary>
        public TimestampDelta GetPassedDurationOrZero(Timestamp time)
        {
            var passed = Now - time;
            return passed.IsPositive ? passed : TimestampDelta.Zero;
        }

        /// <summary>Signed duration until <paramref name="end"/> (negative once it has passed).</summary>
        public TimestampDelta GetRemainingDuration(Timestamp end) => end - Now;

        /// <summary>Time left until <paramref name="end"/>, never negative.</summary>
        public TimestampDelta GetRemainingDurationOrZero(Timestamp end)
        {
            var remaining = end - Now;
            return remaining.IsPositive ? remaining : TimestampDelta.Zero;
        }

        #endregion

        #region Progress

        /// <summary>Progress of <see cref="Now"/> from <paramref name="start"/> to <paramref name="end"/>, clamped to [0,1].</summary>
        public float GetCurrentProgressClamped(Timestamp start, Timestamp end)
        {
            double progress = GetCurrentProgressUnClamped(start, end);
            if (progress < 0) progress = 0;
            else if (progress > 1) progress = 1;
            return (float)progress;
        }

        /// <summary>Progress of <see cref="Now"/> from <paramref name="start"/> to <paramref name="end"/>, un-clamped (may exit [0,1]).</summary>
        public float GetCurrentProgressUnClamped(Timestamp start, Timestamp end)
        {
            double span = end.UnixSeconds - start.UnixSeconds;
            if (span == 0) return Now >= end ? 1f : 0f;
            return (float)((Now.UnixSeconds - start.UnixSeconds) / span);
        }

        /// <summary>Un-clamped progress of an operation known by its <paramref name="end"/> and <paramref name="totalDuration"/>.</summary>
        public float GetCurrentProgressUnClamped(Timestamp end, TimestampDelta totalDuration) =>
            GetCurrentProgressUnClamped(end - totalDuration, end);

        /// <summary>Clamped [0,1] progress of an operation known by its <paramref name="end"/> and <paramref name="totalDuration"/>.</summary>
        public float GetCurrentProgressClamped(Timestamp end, TimestampDelta totalDuration) =>
            GetCurrentProgressClamped(end - totalDuration, end);

        #endregion

        #region Start / end time

        public Timestamp CalculateEndTime(TimeSpan duration) => Now + TimestampDelta.FromTimeSpan(duration);
        public Timestamp CalculateEndTime(TimestampDelta duration) => Now + duration;
        /// <summary>End time rounded up to a whole second (so the operation never appears to finish early).</summary>
        public Timestamp CalculateEndTimeRoundedUp(TimestampDelta duration) => (Now + duration).CeilToSecond();

        public Timestamp CalculateStartTime(TimeSpan duration) => Now - TimestampDelta.FromTimeSpan(duration);
        public Timestamp CalculateStartTime(TimestampDelta duration) => Now - duration;
        /// <summary>Start time rounded down to a whole second (so a past instant never lands in the future).</summary>
        public Timestamp CalculateStartTimeRoundedDown(TimestampDelta duration) => (Now - duration).FloorToSecond();

        #endregion

        #region Periods

        /// <summary>Total duration from the Unix epoch to <see cref="Now"/>.</summary>
        public TimestampDelta GetDurationSinceEpoch() => Now.GetDurationSinceEpoch();

        /// <summary>Which fixed-length <paramref name="period"/> bucket <see cref="Now"/> falls in, counting from the epoch.</summary>
        public long GetCurrentPeriodIndex(TimestampDelta period) => Now.GetPeriodIndex(period);

        /// <summary>Current period index wrapped into [0, <paramref name="maxIndexExclusive"/>) — cyclic reset slots.</summary>
        public long GetCurrentPeriodIndex(TimestampDelta period, long maxIndexExclusive) =>
            GetCurrentPeriodIndex(period) % maxIndexExclusive;

        /// <summary>Signed count of whole <paramref name="period"/>s between <paramref name="startTime"/> and now.</summary>
        public long GetPassedPeriod(TimestampDelta period, Timestamp startTime) =>
            GetCurrentPeriodIndex(period) - startTime.GetPeriodIndex(period);

        /// <summary>Whole <paramref name="period"/>s passed since <paramref name="startTime"/>, never negative.</summary>
        public long GetPassedPeriodOrZero(TimestampDelta period, Timestamp startTime)
        {
            long passed = GetPassedPeriod(period, startTime);
            return passed > 0 ? passed : 0;
        }

        /// <summary>Signed count of whole <paramref name="period"/>s between now and <paramref name="endTime"/>.</summary>
        public long GetRemainingPeriod(TimestampDelta period, Timestamp endTime) =>
            endTime.GetPeriodIndex(period) - GetCurrentPeriodIndex(period);

        /// <summary>Whole <paramref name="period"/>s remaining until <paramref name="endTime"/>, never negative.</summary>
        public long GetRemainingPeriodOrZero(TimestampDelta period, Timestamp endTime)
        {
            long remaining = GetRemainingPeriod(period, endTime);
            return remaining > 0 ? remaining : 0;
        }

        #endregion

        #region Day / week boundaries

        /// <summary>Whole days since the Unix epoch for <paramref name="time"/> (UTC day index).</summary>
        public long GetDayIndex(Timestamp time) => (long)Math.Floor(time.UnixSeconds / SecondsPerDay);

        /// <summary>The UTC midnight at or before <paramref name="time"/>.</summary>
        public Timestamp GetDayStart(Timestamp time) => new Timestamp(Math.Floor(time.UnixSeconds / SecondsPerDay) * SecondsPerDay);

        /// <summary>The UTC calendar-date midnight of the current day.</summary>
        public Timestamp GetCurrentDayStartTime() => Now.GetStartOfDay();

        /// <summary>The next UTC midnight after <see cref="Now"/> (e.g. a daily-reward reset gate).</summary>
        public Timestamp GetNextDayStartTime() => GetDayStart(Now) + TimestampDelta.FromSeconds(SecondsPerDay);

        /// <summary>
        /// The next UTC midnight that lands on <paramref name="dayOfWeek"/> (strictly in the future —
        /// if today already is that weekday, returns the one a week out). A weekly-reset boundary.
        /// </summary>
        public Timestamp GetStartOfTargetDay(DayOfWeek dayOfWeek)
        {
            var today = Now.ToUtc();
            int daysUntil = ((int)dayOfWeek - (int)today.DayOfWeek + 7) % 7;
            if (daysUntil == 0) daysUntil = 7;
            return Timestamp.FromUtc(today.AddDays(daysUntil).Date);
        }

        #endregion

        #region Drift diagnostics

        /// <summary>Signed gap between the wall clock and the ticking clock (<see cref="TickIndependentNow"/> − <see cref="Now"/>).</summary>
        public TimestampDelta GetDriftError() => TickIndependentNow - Now;

        /// <summary>Whether the current drift is within <see cref="DriftToleranceSeconds"/>.</summary>
        public bool IsDriftWithinTolerance() => Math.Abs(GetDriftError().Seconds) <= DriftToleranceSeconds;

        /// <summary>
        /// Reports drift through <see cref="DriftLogger"/> the first time it exceeds tolerance; a no-op
        /// thereafter. Returns whether drift was within tolerance at this call.
        /// </summary>
        public bool CheckDriftWithinTolerance()
        {
            if (IsDriftWithinTolerance()) return true;
            if (!_driftReported)
            {
                _driftReported = true;
                DriftLogger($"EpochClock drift {GetDriftError().Seconds}s exceeds tolerance {DriftToleranceSeconds}s (Now {Now}, wall {TickIndependentNow}).");
            }
            return false;
        }

        #endregion
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
            double advance = delta * TimeScale;
            _accumulatedScaled += advance;
            SetLastTickDelta(new TimestampDelta(advance));
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

        public void AddDelta(TimestampDelta delta) { _nowUnixSeconds += delta.Seconds; SetLastTickDelta(delta); }
        public void AddSeconds(double seconds) { _nowUnixSeconds += seconds; SetLastTickDelta(new TimestampDelta(seconds)); }
        public void SetNow(Timestamp time) => _nowUnixSeconds = time.UnixSeconds;
    }
}
