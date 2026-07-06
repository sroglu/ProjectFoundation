using System;

namespace PFound.Utilities.DataType
{
    /// <summary>
    /// A Unix timestamp (seconds elapsed since the 1970-01-01 UTC epoch) with conversions to and from
    /// <see cref="System.DateTime"/>. Engine-free.
    /// </summary>
    [Serializable]
    public struct UnixTime : IEquatable<UnixTime>, IComparable<UnixTime>
    {
        /// <summary>The Unix epoch: 1970-01-01 00:00:00 UTC.</summary>
        public static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>Seconds since the epoch.</summary>
        public long Seconds;

        public UnixTime(long seconds)
        {
            Seconds = seconds;
        }

        /// <summary>The current moment as a Unix timestamp.</summary>
        public static UnixTime Now => new UnixTime(ToUnixTime(DateTime.UtcNow));

        /// <summary>The current moment expressed in whole milliseconds since the epoch.</summary>
        public static long NowMilliseconds => ToUnixTimeMilliseconds(DateTime.UtcNow);

        /// <summary>Converts a <see cref="DateTime"/> to whole seconds since the epoch (interpreted as UTC).</summary>
        public static long ToUnixTime(DateTime dateTime) =>
            (long)(dateTime.ToUniversalTime() - Epoch).TotalSeconds;

        /// <summary>Converts a <see cref="DateTime"/> to whole milliseconds since the epoch.</summary>
        public static long ToUnixTimeMilliseconds(DateTime dateTime) =>
            (long)(dateTime.ToUniversalTime() - Epoch).TotalMilliseconds;

        /// <summary>Builds a UTC <see cref="DateTime"/> from a seconds-since-epoch value.</summary>
        public static DateTime ConvertFrom(long unixSeconds) => Epoch.AddSeconds(unixSeconds);

        /// <summary>Builds a UTC <see cref="DateTime"/> from a milliseconds-since-epoch value.</summary>
        public static DateTime ConvertFromMilliseconds(long unixMilliseconds) => Epoch.AddMilliseconds(unixMilliseconds);

        /// <summary>Converts a <see cref="DateTime"/> to seconds since the epoch. Alias of <see cref="ToUnixTime"/>.</summary>
        public static long ConvertTo(DateTime dateTime) => ToUnixTime(dateTime);

        /// <summary>This timestamp as a UTC <see cref="DateTime"/>.</summary>
        public DateTime ToDateTime() => ConvertFrom(Seconds);

        public static implicit operator long(UnixTime time) => time.Seconds;

        public static implicit operator UnixTime(long seconds) => new UnixTime(seconds);

        public int CompareTo(UnixTime other) => Seconds.CompareTo(other.Seconds);

        public bool Equals(UnixTime other) => Seconds == other.Seconds;

        public override bool Equals(object obj) => obj is UnixTime other && Equals(other);

        public override int GetHashCode() => Seconds.GetHashCode();

        public static bool operator ==(UnixTime a, UnixTime b) => a.Seconds == b.Seconds;

        public static bool operator !=(UnixTime a, UnixTime b) => a.Seconds != b.Seconds;

        public override string ToString() => Seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
