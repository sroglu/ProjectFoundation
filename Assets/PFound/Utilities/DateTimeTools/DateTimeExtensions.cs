using System;
using System.Collections.Generic;
using System.Globalization;

namespace PFound.Utilities.DateTimeTools
{
    /// <summary>
    /// Calendar-boundary and navigation helpers for <see cref="DateTime"/>.
    /// Boundaries preserve the original <see cref="DateTime.Kind"/>.
    /// </summary>
    public static class DateTimeExtensions
    {
        // One tick before the next day; the largest representable time within a day.
        private static readonly TimeSpan DayEndOffset = TimeSpan.FromDays(1) - TimeSpan.FromTicks(1);

        /// <summary>Midnight (00:00:00.000) of the same calendar day.</summary>
        public static DateTime StartOfDay(this DateTime value)
        {
            return new DateTime(value.Year, value.Month, value.Day, 0, 0, 0, value.Kind);
        }

        /// <summary>The final tick (23:59:59.9999999) of the same calendar day.</summary>
        public static DateTime EndOfDay(this DateTime value)
        {
            return value.StartOfDay().Add(DayEndOffset);
        }

        /// <summary>
        /// Removes the time-of-day component, equivalent to <see cref="StartOfDay"/>
        /// but named for the "clear time" intent.
        /// </summary>
        public static DateTime ClearTime(this DateTime value)
        {
            return value.StartOfDay();
        }

        /// <summary>
        /// The first moment of the week that contains <paramref name="value"/>,
        /// using <paramref name="firstDayOfWeek"/> as the week's start.
        /// </summary>
        public static DateTime StartOfWeek(this DateTime value, DayOfWeek firstDayOfWeek = DayOfWeek.Monday)
        {
            int delta = (7 + (value.DayOfWeek - firstDayOfWeek)) % 7;
            return value.StartOfDay().AddDays(-delta);
        }

        /// <summary>The final tick of the week that contains <paramref name="value"/>.</summary>
        public static DateTime EndOfWeek(this DateTime value, DayOfWeek firstDayOfWeek = DayOfWeek.Monday)
        {
            return value.StartOfWeek(firstDayOfWeek).AddDays(7).AddTicks(-1);
        }

        /// <summary>Midnight of the first day of the month.</summary>
        public static DateTime StartOfMonth(this DateTime value)
        {
            return new DateTime(value.Year, value.Month, 1, 0, 0, 0, value.Kind);
        }

        /// <summary>The final tick of the last day of the month.</summary>
        public static DateTime EndOfMonth(this DateTime value)
        {
            return value.StartOfMonth().AddMonths(1).AddTicks(-1);
        }

        /// <summary>Midnight of January 1st of the same year.</summary>
        public static DateTime StartOfYear(this DateTime value)
        {
            return new DateTime(value.Year, 1, 1, 0, 0, 0, value.Kind);
        }

        /// <summary>The final tick of December 31st of the same year.</summary>
        public static DateTime EndOfYear(this DateTime value)
        {
            return value.StartOfYear().AddYears(1).AddTicks(-1);
        }

        /// <summary>
        /// The next occurrence of <paramref name="dayOfWeek"/> strictly after
        /// <paramref name="value"/>'s day (time-of-day is cleared).
        /// </summary>
        public static DateTime Next(this DateTime value, DayOfWeek dayOfWeek)
        {
            int delta = (7 + (dayOfWeek - value.DayOfWeek) - 1) % 7 + 1;
            return value.StartOfDay().AddDays(delta);
        }

        /// <summary>
        /// The previous occurrence of <paramref name="dayOfWeek"/> strictly before
        /// <paramref name="value"/>'s day (time-of-day is cleared).
        /// </summary>
        public static DateTime Previous(this DateTime value, DayOfWeek dayOfWeek)
        {
            int delta = (7 + (value.DayOfWeek - dayOfWeek) - 1) % 7 + 1;
            return value.StartOfDay().AddDays(-delta);
        }

        /// <summary>
        /// Enumerates one <see cref="DateTime"/> per calendar day from
        /// <paramref name="from"/> to <paramref name="to"/> inclusive, at midnight.
        /// Iterates forward or backward depending on the order of the bounds.
        /// </summary>
        public static IEnumerable<DateTime> EachDay(DateTime from, DateTime to)
        {
            DateTime start = from.StartOfDay();
            DateTime end = to.StartOfDay();
            int step = start <= end ? 1 : -1;

            for (DateTime day = start; step > 0 ? day <= end : day >= end; day = day.AddDays(step))
                yield return day;
        }

        /// <summary>
        /// Formats the value into a string that is safe to embed in a file name
        /// (no colons or other reserved characters), for example
        /// <c>2026-07-06_14-32-05</c>.
        /// </summary>
        public static string ToFileNameSafeString(this DateTime value)
        {
            return value.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
        }
    }
}
