using System;
using System.Globalization;

namespace PFound.Utilities.DateTimeTools
{
    /// <summary>
    /// Human-readable formatters for <see cref="TimeSpan"/> durations.
    /// </summary>
    public static class TimeSpanExtensions
    {
        /// <summary>
        /// Formats as <c>mm:ss</c> (total minutes, zero-padded seconds), for
        /// example <c>03:07</c> or <c>125:00</c> for durations over an hour.
        /// Negative durations are prefixed with a minus sign.
        /// </summary>
        public static string ToMinutesSeconds(this TimeSpan span)
        {
            string sign = span < TimeSpan.Zero ? "-" : string.Empty;
            TimeSpan abs = span.Duration();
            int totalMinutes = (int)Math.Floor(abs.TotalMinutes);
            return string.Format(CultureInfo.InvariantCulture,
                "{0}{1:00}:{2:00}", sign, totalMinutes, abs.Seconds);
        }

        /// <summary>
        /// Formats as <c>mm:ss.fff</c>, adding zero-padded milliseconds to
        /// <see cref="ToMinutesSeconds"/>.
        /// </summary>
        public static string ToMinutesSecondsMillis(this TimeSpan span)
        {
            string sign = span < TimeSpan.Zero ? "-" : string.Empty;
            TimeSpan abs = span.Duration();
            int totalMinutes = (int)Math.Floor(abs.TotalMinutes);
            return string.Format(CultureInfo.InvariantCulture,
                "{0}{1:00}:{2:00}.{3:000}", sign, totalMinutes, abs.Seconds, abs.Milliseconds);
        }

        /// <summary>
        /// Formats as <c>h:mm:ss</c> using total hours, for example
        /// <c>1:05:09</c> or <c>27:00:00</c>. Negative durations are prefixed
        /// with a minus sign.
        /// </summary>
        public static string ToHoursMinutesSeconds(this TimeSpan span)
        {
            string sign = span < TimeSpan.Zero ? "-" : string.Empty;
            TimeSpan abs = span.Duration();
            int totalHours = (int)Math.Floor(abs.TotalHours);
            return string.Format(CultureInfo.InvariantCulture,
                "{0}{1}:{2:00}:{3:00}", sign, totalHours, abs.Minutes, abs.Seconds);
        }
    }
}
