using System;

namespace PFound.EpochClock
{
    /// <summary>
    /// A point in time as seconds since the Unix epoch (UTC), double precision. Arithmetic with
    /// <see cref="TimestampDelta"/> and full ordering; the foundation type the clock reports.
    /// </summary>
    public readonly struct Timestamp : IEquatable<Timestamp>, IComparable<Timestamp>
    {
        public readonly double UnixSeconds;
        public Timestamp(double unixSeconds) { UnixSeconds = unixSeconds; }

        public static readonly Timestamp Zero = new Timestamp(0);

        public static Timestamp FromUnixSeconds(double seconds) => new Timestamp(seconds);
        public static Timestamp FromUtc(DateTime utc) => new Timestamp(new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeMilliseconds() / 1000.0);
        public DateTime ToUtc() => DateTimeOffset.FromUnixTimeMilliseconds((long)(UnixSeconds * 1000.0)).UtcDateTime;

        public static Timestamp operator +(Timestamp t, TimestampDelta d) => new Timestamp(t.UnixSeconds + d.Seconds);
        public static Timestamp operator -(Timestamp t, TimestampDelta d) => new Timestamp(t.UnixSeconds - d.Seconds);
        public static TimestampDelta operator -(Timestamp a, Timestamp b) => new TimestampDelta(a.UnixSeconds - b.UnixSeconds);

        public static bool operator <(Timestamp a, Timestamp b) => a.UnixSeconds < b.UnixSeconds;
        public static bool operator >(Timestamp a, Timestamp b) => a.UnixSeconds > b.UnixSeconds;
        public static bool operator <=(Timestamp a, Timestamp b) => a.UnixSeconds <= b.UnixSeconds;
        public static bool operator >=(Timestamp a, Timestamp b) => a.UnixSeconds >= b.UnixSeconds;
        public static bool operator ==(Timestamp a, Timestamp b) => a.UnixSeconds == b.UnixSeconds;
        public static bool operator !=(Timestamp a, Timestamp b) => a.UnixSeconds != b.UnixSeconds;

        public int CompareTo(Timestamp other) => UnixSeconds.CompareTo(other.UnixSeconds);
        public bool Equals(Timestamp other) => UnixSeconds == other.UnixSeconds;
        public override bool Equals(object obj) => obj is Timestamp other && Equals(other);
        public override int GetHashCode() => UnixSeconds.GetHashCode();
        public override string ToString() => "@" + UnixSeconds;
    }
}
