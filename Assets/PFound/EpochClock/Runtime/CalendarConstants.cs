using System;

namespace PFound.EpochClock
{
    /// <summary>
    /// Fixed calendar arithmetic table: how many seconds/minutes/hours fit in a day/week/month/year,
    /// plus ready-made <see cref="TimeSpan"/> unit values. Month/year figures use nominal 30-day /
    /// 365-day approximations (no leap-year or variable-month accuracy) — adequate for game cadence
    /// (daily/weekly resets, cooldowns), not for civil-calendar math. Pure C — no engine dependency.
    /// </summary>
    public static class CalendarConstants
    {
        /// <summary>Default clock hour used when a game day needs a canonical mid-day anchor.</summary>
        public const int DefaultHourOfDay = 12;

        /// <summary>Default real-seconds length of one compressed in-game day (see <see cref="Timestamp.ToScaledDayTime"/>).</summary>
        public const int DefaultDayLengthSeconds = 900; // 15 minutes

        public const int SecondsInMinute = 60;
        public const int MinutesInHour = 60;
        public const int HoursInDay = 24;
        public const int DaysInWeek = 7;
        public const int DaysInMonth = 30;   // approximate
        public const int DaysInYear = 365;   // approximate
        public const int WeeksInMonth = 4;   // approximate
        public const int WeeksInYear = 52;   // approximate
        public const int MonthsInYear = 12;

        public const int SecondsInHour = SecondsInMinute * MinutesInHour;
        public const int SecondsInDay = SecondsInHour * HoursInDay;
        public const int SecondsInWeek = SecondsInDay * DaysInWeek;
        public const int SecondsInMonth = SecondsInDay * DaysInMonth; // approximate
        public const int SecondsInYear = SecondsInDay * DaysInYear;   // approximate

        public const int MinutesInDay = MinutesInHour * HoursInDay;
        public const int MinutesInWeek = MinutesInDay * DaysInWeek;
        public const int MinutesInMonth = MinutesInDay * DaysInMonth; // approximate
        public const int MinutesInYear = MinutesInDay * DaysInYear;   // approximate

        public const int HoursInWeek = HoursInDay * DaysInWeek;
        public const int HoursInMonth = HoursInDay * DaysInMonth; // approximate
        public const int HoursInYear = HoursInDay * DaysInYear;   // approximate

        public static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);
        public static readonly TimeSpan OneMinute = TimeSpan.FromMinutes(1);
        public static readonly TimeSpan OneHour = TimeSpan.FromHours(1);
        public static readonly TimeSpan OneDay = TimeSpan.FromDays(1);
        public static readonly TimeSpan OneWeek = TimeSpan.FromDays(DaysInWeek);
        public static readonly TimeSpan OneMonth = TimeSpan.FromDays(DaysInMonth); // approximate
        public static readonly TimeSpan OneYear = TimeSpan.FromDays(DaysInYear);   // approximate
    }
}
