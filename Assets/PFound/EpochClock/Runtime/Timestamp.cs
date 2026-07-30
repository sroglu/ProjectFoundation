using System;

namespace PFound.EpochClock
{
    /// <summary>
    /// A point in time as seconds since the Unix epoch (UTC), double precision. Arithmetic with
    /// <see cref="TimestampDelta"/> and full ordering; the foundation type the clock reports.
    /// </summary>
    [Serializable]
    public readonly struct Timestamp : IEquatable<Timestamp>, IComparable<Timestamp>
    {
        public readonly double UnixSeconds;
        public Timestamp(double unixSeconds) { UnixSeconds = unixSeconds; }

        #region Constants

        public static readonly Timestamp Zero = new Timestamp(0);
        public static readonly Timestamp MinValue = new Timestamp(double.MinValue);
        public static readonly Timestamp MaxValue = new Timestamp(double.MaxValue);

        #endregion

        #region Factories / conversion

        public static Timestamp FromUnixSeconds(double seconds) => new Timestamp(seconds);
        public static Timestamp FromUtc(DateTime utc) => new Timestamp(new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeMilliseconds() / 1000.0);
        public DateTime ToUtc() => DateTimeOffset.FromUnixTimeMilliseconds((long)(UnixSeconds * 1000.0)).UtcDateTime;

        /// <summary>The largest whole-second instant at or before this one.</summary>
        public Timestamp FloorToSecond() => new Timestamp(Math.Floor(UnixSeconds));
        /// <summary>The smallest whole-second instant at or after this one.</summary>
        public Timestamp CeilToSecond() => new Timestamp(Math.Ceiling(UnixSeconds));
        /// <summary>The nearest whole-second instant.</summary>
        public Timestamp RoundToSecond() => new Timestamp(Math.Round(UnixSeconds));
        /// <summary>Milliseconds since the epoch, floored — for stable integer keys / wire payloads.</summary>
        public ulong ToFloorRoundedMilliseconds() => (ulong)Math.Floor(UnixSeconds * 1000.0);

        #endregion

        #region Math

        public static Timestamp Lerp(Timestamp start, Timestamp end, double t) =>
            new Timestamp(start.UnixSeconds + (end.UnixSeconds - start.UnixSeconds) * t);
        /// <summary>Inverse of <see cref="Lerp"/>: where <paramref name="x"/> falls between start and end, un-clamped.</summary>
        public static double Unlerp(Timestamp start, Timestamp end, Timestamp x) =>
            (x.UnixSeconds - start.UnixSeconds) / (end.UnixSeconds - start.UnixSeconds);
        public static Timestamp Clamp(Timestamp value, Timestamp min, Timestamp max) =>
            new Timestamp(value.UnixSeconds < min.UnixSeconds ? min.UnixSeconds : value.UnixSeconds > max.UnixSeconds ? max.UnixSeconds : value.UnixSeconds);
        public static Timestamp Max(Timestamp a, Timestamp b) => a.UnixSeconds >= b.UnixSeconds ? a : b;
        public static Timestamp Min(Timestamp a, Timestamp b) => a.UnixSeconds <= b.UnixSeconds ? a : b;

        #endregion

        #region Operators

        public static Timestamp operator +(Timestamp t, TimestampDelta d) => new Timestamp(t.UnixSeconds + d.Seconds);
        public static Timestamp operator -(Timestamp t, TimestampDelta d) => new Timestamp(t.UnixSeconds - d.Seconds);
        public static TimestampDelta operator -(Timestamp a, Timestamp b) => new TimestampDelta(a.UnixSeconds - b.UnixSeconds);

        public static bool operator <(Timestamp a, Timestamp b) => a.UnixSeconds < b.UnixSeconds;
        public static bool operator >(Timestamp a, Timestamp b) => a.UnixSeconds > b.UnixSeconds;
        public static bool operator <=(Timestamp a, Timestamp b) => a.UnixSeconds <= b.UnixSeconds;
        public static bool operator >=(Timestamp a, Timestamp b) => a.UnixSeconds >= b.UnixSeconds;
        public static bool operator ==(Timestamp a, Timestamp b) => a.UnixSeconds == b.UnixSeconds;
        public static bool operator !=(Timestamp a, Timestamp b) => a.UnixSeconds != b.UnixSeconds;

        #endregion

        #region Periods & calendar boundaries

        /// <summary>The signed duration from the Unix epoch to this instant.</summary>
        public TimestampDelta GetDurationSinceEpoch() => this - Zero;

        /// <summary>
        /// Which fixed-length <paramref name="period"/> bucket this instant falls in, counting from the
        /// epoch (e.g. day/week index for daily/weekly resets). Truncates toward the epoch.
        /// </summary>
        public long GetPeriodIndex(TimestampDelta period)
        {
            if (period.Seconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(period), "Period must be a positive duration.");
            return (long)(GetDurationSinceEpoch() / period);
        }

        /// <summary>The UTC calendar-date midnight at or before this instant.</summary>
        public Timestamp GetStartOfDay() => FromUtc(ToUtc().Date);

        /// <summary>The UTC calendar-date midnight of the following day.</summary>
        public Timestamp GetStartOfNextDay() => GetStartOfDay() + TimestampDelta.OneDay;

        /// <summary>
        /// Maps this instant onto a compressed in-game day of length <paramref name="dayLengthInSeconds"/>
        /// real seconds, returning the time-of-day within that scaled day (wraps at the scaled day end).
        /// </summary>
        public TimeSpan ToScaledDayTime(double dayLengthInSeconds)
        {
            double scaleModifier = CalendarConstants.SecondsInDay / dayLengthInSeconds;
            double realTimeOfDay = ToUtc().TimeOfDay.TotalSeconds;
            return TimeSpan.FromSeconds(realTimeOfDay * scaleModifier % CalendarConstants.SecondsInDay);
        }

        #endregion

        #region Equality / ordering / parsing

        public int CompareTo(Timestamp other) => UnixSeconds.CompareTo(other.UnixSeconds);
        public bool Equals(Timestamp other) => UnixSeconds == other.UnixSeconds;
        public override bool Equals(object obj) => obj is Timestamp other && Equals(other);
        public override int GetHashCode() => UnixSeconds.GetHashCode();
        public override string ToString() => "@" + UnixSeconds;

        public static bool TryParse(string text, out Timestamp value)
        {
            if (double.TryParse(text, out double seconds))
            {
                value = new Timestamp(seconds);
                return true;
            }
            value = Zero;
            return false;
        }

        #endregion
    }
}
