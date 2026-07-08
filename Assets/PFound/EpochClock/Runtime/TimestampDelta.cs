using System;

namespace PFound.EpochClock
{
    /// <summary>A signed duration in seconds (double precision). The difference between two <see cref="Timestamp"/>s.</summary>
    [Serializable]
    public readonly struct TimestampDelta : IEquatable<TimestampDelta>, IComparable<TimestampDelta>
    {
        public readonly double Seconds;
        public TimestampDelta(double seconds) { Seconds = seconds; }

        #region Constants

        public static readonly TimestampDelta Zero = new TimestampDelta(0);
        public static readonly TimestampDelta OneSecond = new TimestampDelta(1);
        public static readonly TimestampDelta OneMinute = new TimestampDelta(CalendarConstants.SecondsInMinute);
        public static readonly TimestampDelta OneHour = new TimestampDelta(CalendarConstants.SecondsInHour);
        public static readonly TimestampDelta OneDay = new TimestampDelta(CalendarConstants.SecondsInDay);
        public static readonly TimestampDelta OneWeek = new TimestampDelta(CalendarConstants.SecondsInWeek);
        public static readonly TimestampDelta OneMonth = new TimestampDelta(CalendarConstants.SecondsInMonth); // approximate
        public static readonly TimestampDelta OneYear = new TimestampDelta(CalendarConstants.SecondsInYear);   // approximate
        public static readonly TimestampDelta MinValue = new TimestampDelta(double.MinValue);
        public static readonly TimestampDelta MaxValue = new TimestampDelta(double.MaxValue);

        #endregion

        #region Factories / conversion

        public static TimestampDelta FromSeconds(double seconds) => new TimestampDelta(seconds);
        public static TimestampDelta FromMinutes(double minutes) => new TimestampDelta(minutes * CalendarConstants.SecondsInMinute);
        public static TimestampDelta FromHours(double hours) => new TimestampDelta(hours * CalendarConstants.SecondsInHour);
        public static TimestampDelta FromDays(double days) => new TimestampDelta(days * CalendarConstants.SecondsInDay);
        public static TimestampDelta FromTimeSpan(TimeSpan span) => new TimestampDelta(span.TotalSeconds);
        public TimeSpan ToTimeSpan() => TimeSpan.FromSeconds(Seconds);

        #endregion

        #region State / decomposition

        public bool IsPositive => Seconds > 0;

        /// <summary>Whole-day component of this duration (does not include remaining hours/minutes/seconds).</summary>
        public long Days => (long)(Seconds / CalendarConstants.SecondsInDay);
        /// <summary>Hour component within the current day, [0,23] for positive durations.</summary>
        public long Hours => (long)(Seconds / CalendarConstants.SecondsInHour) % CalendarConstants.HoursInDay;
        /// <summary>Minute component within the current hour, [0,59] for positive durations.</summary>
        public long Minutes => (long)(Seconds / CalendarConstants.SecondsInMinute) % CalendarConstants.MinutesInHour;

        /// <summary>Total whole days spanned by this duration.</summary>
        public long TotalDays => (long)(Seconds / CalendarConstants.SecondsInDay);
        /// <summary>Total whole hours spanned by this duration.</summary>
        public long TotalHours => (long)(Seconds / CalendarConstants.SecondsInHour);
        /// <summary>Total whole minutes spanned by this duration.</summary>
        public long TotalMinutes => (long)(Seconds / CalendarConstants.SecondsInMinute);

        #endregion

        #region Math

        public static TimestampDelta Clamp(TimestampDelta value, TimestampDelta min, TimestampDelta max) =>
            new TimestampDelta(value.Seconds < min.Seconds ? min.Seconds : value.Seconds > max.Seconds ? max.Seconds : value.Seconds);
        public static TimestampDelta Max(TimestampDelta a, TimestampDelta b) => a.Seconds >= b.Seconds ? a : b;
        public static TimestampDelta Min(TimestampDelta a, TimestampDelta b) => a.Seconds <= b.Seconds ? a : b;

        #endregion

        #region Operators

        public static TimestampDelta operator +(TimestampDelta a, TimestampDelta b) => new TimestampDelta(a.Seconds + b.Seconds);
        public static TimestampDelta operator -(TimestampDelta a, TimestampDelta b) => new TimestampDelta(a.Seconds - b.Seconds);
        public static TimestampDelta operator -(TimestampDelta d) => new TimestampDelta(-d.Seconds);

        public static TimestampDelta operator *(TimestampDelta d, double scale) => new TimestampDelta(d.Seconds * scale);
        public static TimestampDelta operator *(double scale, TimestampDelta d) => new TimestampDelta(d.Seconds * scale);
        public static TimestampDelta operator *(TimestampDelta d, int scale) => new TimestampDelta(d.Seconds * scale);
        public static TimestampDelta operator *(int scale, TimestampDelta d) => new TimestampDelta(d.Seconds * scale);
        public static TimestampDelta operator *(TimestampDelta d, long scale) => new TimestampDelta(d.Seconds * scale);
        public static TimestampDelta operator *(long scale, TimestampDelta d) => new TimestampDelta(d.Seconds * scale);

        public static TimestampDelta operator /(TimestampDelta d, double divisor) => new TimestampDelta(d.Seconds / divisor);
        public static TimestampDelta operator /(TimestampDelta d, int divisor) => new TimestampDelta(d.Seconds / divisor);
        public static TimestampDelta operator /(TimestampDelta d, long divisor) => new TimestampDelta(d.Seconds / divisor);
        /// <summary>Dimensionless ratio: how many times <paramref name="b"/> fits into <paramref name="a"/>.</summary>
        public static double operator /(TimestampDelta a, TimestampDelta b) => a.Seconds / b.Seconds;
        public static TimestampDelta operator %(TimestampDelta a, TimestampDelta b) => new TimestampDelta(a.Seconds % b.Seconds);

        public static bool operator <(TimestampDelta a, TimestampDelta b) => a.Seconds < b.Seconds;
        public static bool operator >(TimestampDelta a, TimestampDelta b) => a.Seconds > b.Seconds;
        public static bool operator <=(TimestampDelta a, TimestampDelta b) => a.Seconds <= b.Seconds;
        public static bool operator >=(TimestampDelta a, TimestampDelta b) => a.Seconds >= b.Seconds;
        public static bool operator ==(TimestampDelta a, TimestampDelta b) => a.Seconds == b.Seconds;
        public static bool operator !=(TimestampDelta a, TimestampDelta b) => a.Seconds != b.Seconds;

        #endregion

        #region Equality / ordering / parsing

        public int CompareTo(TimestampDelta other) => Seconds.CompareTo(other.Seconds);
        public bool Equals(TimestampDelta other) => Seconds == other.Seconds;
        public override bool Equals(object obj) => obj is TimestampDelta other && Equals(other);
        public override int GetHashCode() => Seconds.GetHashCode();
        public override string ToString() => Seconds + "s";

        public static bool TryParse(string text, out TimestampDelta value)
        {
            if (double.TryParse(text, out double seconds))
            {
                value = new TimestampDelta(seconds);
                return true;
            }
            value = Zero;
            return false;
        }

        #endregion
    }
}
