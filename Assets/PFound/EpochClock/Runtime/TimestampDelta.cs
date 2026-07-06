using System;

namespace PFound.EpochClock
{
    /// <summary>A signed duration in seconds (double precision). The difference between two <see cref="Timestamp"/>s.</summary>
    public readonly struct TimestampDelta : IEquatable<TimestampDelta>, IComparable<TimestampDelta>
    {
        public readonly double Seconds;
        public TimestampDelta(double seconds) { Seconds = seconds; }

        public static readonly TimestampDelta Zero = new TimestampDelta(0);

        public static TimestampDelta FromSeconds(double seconds) => new TimestampDelta(seconds);
        public static TimestampDelta FromTimeSpan(TimeSpan span) => new TimestampDelta(span.TotalSeconds);
        public TimeSpan ToTimeSpan() => TimeSpan.FromSeconds(Seconds);

        public bool IsPositive => Seconds > 0;

        public static TimestampDelta operator +(TimestampDelta a, TimestampDelta b) => new TimestampDelta(a.Seconds + b.Seconds);
        public static TimestampDelta operator -(TimestampDelta a, TimestampDelta b) => new TimestampDelta(a.Seconds - b.Seconds);
        public static TimestampDelta operator *(TimestampDelta d, double scale) => new TimestampDelta(d.Seconds * scale);

        public static bool operator <(TimestampDelta a, TimestampDelta b) => a.Seconds < b.Seconds;
        public static bool operator >(TimestampDelta a, TimestampDelta b) => a.Seconds > b.Seconds;
        public static bool operator <=(TimestampDelta a, TimestampDelta b) => a.Seconds <= b.Seconds;
        public static bool operator >=(TimestampDelta a, TimestampDelta b) => a.Seconds >= b.Seconds;
        public static bool operator ==(TimestampDelta a, TimestampDelta b) => a.Seconds == b.Seconds;
        public static bool operator !=(TimestampDelta a, TimestampDelta b) => a.Seconds != b.Seconds;

        public int CompareTo(TimestampDelta other) => Seconds.CompareTo(other.Seconds);
        public bool Equals(TimestampDelta other) => Seconds == other.Seconds;
        public override bool Equals(object obj) => obj is TimestampDelta other && Equals(other);
        public override int GetHashCode() => Seconds.GetHashCode();
        public override string ToString() => Seconds + "s";
    }
}
