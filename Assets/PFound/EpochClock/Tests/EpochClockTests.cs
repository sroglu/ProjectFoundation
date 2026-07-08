using System;
using PFound.EpochClock;

// Standalone mono/csc runner (pure C#) — parity oracle for the epoch clock.
internal static class EpochClockTests
{
    private static int s_passed, s_failed;
    private static void Check(bool cond, string name)
    {
        if (cond) s_passed++;
        else { s_failed++; Console.WriteLine("  FAIL: " + name); }
    }

    private static bool Near(double a, double b) => Math.Abs(a - b) < 1e-6;

    public static int Main()
    {
        // Timestamp / TimestampDelta arithmetic
        var t0 = Timestamp.FromUnixSeconds(1000);
        var t1 = t0 + TimestampDelta.FromSeconds(60);
        Check(Near(t1.UnixSeconds, 1060), "Timestamp + Delta");
        Check(Near((t1 - t0).Seconds, 60), "Timestamp - Timestamp = Delta");
        Check(t1 > t0 && t0 < t1 && t0 != t1, "Timestamp ordering");
        Check(Near(TimestampDelta.FromTimeSpan(TimeSpan.FromMinutes(2)).Seconds, 120), "Delta from TimeSpan");

        // TestEpochClock advances only manually
        var clock = new TestEpochClock(Timestamp.FromUnixSeconds(1000));
        Check(Near(clock.Now.UnixSeconds, 1000), "TestEpochClock starts at given time");
        clock.AddSeconds(50);
        Check(Near(clock.Now.UnixSeconds, 1050), "AddSeconds advances Now");

        // IsPassed / IsBetween
        Check(clock.IsPassed(Timestamp.FromUnixSeconds(1000)) && !clock.IsPassed(Timestamp.FromUnixSeconds(2000)), "IsPassed");
        Check(clock.IsBetween(Timestamp.FromUnixSeconds(1000), Timestamp.FromUnixSeconds(1100)), "IsBetween");

        // remaining duration, clamped to zero
        Check(Near(clock.GetRemainingDurationOrZero(Timestamp.FromUnixSeconds(1100)).Seconds, 50), "remaining duration");
        Check(clock.GetRemainingDurationOrZero(Timestamp.FromUnixSeconds(1000)) == TimestampDelta.Zero, "remaining clamps to zero when past");

        // progress clamped
        var start = Timestamp.FromUnixSeconds(1000);
        var end = Timestamp.FromUnixSeconds(1100);
        clock.SetNow(Timestamp.FromUnixSeconds(1050));
        Check(Near(clock.GetCurrentProgressClamped(start, end), 0.5f), "progress 0.5 mid-range");
        clock.SetNow(Timestamp.FromUnixSeconds(900));
        Check(Near(clock.GetCurrentProgressClamped(start, end), 0f), "progress clamps to 0 before start");
        clock.SetNow(Timestamp.FromUnixSeconds(1200));
        Check(Near(clock.GetCurrentProgressClamped(start, end), 1f), "progress clamps to 1 after end");

        // CalculateEndTime
        clock.SetNow(Timestamp.FromUnixSeconds(1000));
        Check(Near(clock.CalculateEndTime(TimeSpan.FromHours(1)).UnixSeconds, 1000 + 3600), "CalculateEndTime adds duration");

        // day index + next-day boundary (UTC midnight)
        var midnight = Timestamp.FromUtc(new DateTime(2026, 6, 25, 0, 0, 0, DateTimeKind.Utc));
        var noon = midnight + TimestampDelta.FromSeconds(43200);
        clock.SetNow(noon);
        Check(clock.GetDayStart(noon) == midnight, "GetDayStart returns UTC midnight");
        Check(clock.GetNextDayStartTime() == midnight + TimestampDelta.FromSeconds(86400), "GetNextDayStartTime is next UTC midnight");
        Check(clock.GetDayIndex(noon) == clock.GetDayIndex(midnight), "GetDayIndex equal within the same day");

        // ClientEpochClock: Now near wall clock; Tick advances by elapsed*scale
        var client = new ClientEpochClock { TimeScale = 0f };
        double before = client.Now.UnixSeconds;
        System.Threading.Thread.Sleep(20);
        client.Tick(); // scale 0 → Now unchanged despite elapsed time
        Check(Near(client.Now.UnixSeconds, before), "ClientEpochClock TimeScale 0 freezes Now");
        client.TimeScale = 1f;
        System.Threading.Thread.Sleep(20);
        client.Tick();
        Check(client.Now.UnixSeconds > before, "ClientEpochClock advances Now after Tick at scale 1");
        Check(client.LastTickDelta.Seconds > 0, "ClientEpochClock records positive LastTickDelta");

        // ---------- CalendarConstants ----------
        Check(CalendarConstants.SecondsInDay == 86400, "SecondsInDay");
        Check(CalendarConstants.SecondsInWeek == 604800, "SecondsInWeek");
        Check(CalendarConstants.SecondsInHour == 3600 && CalendarConstants.MinutesInDay == 1440, "hour/minute table");
        Check(CalendarConstants.HoursInWeek == 168 && CalendarConstants.DaysInYear == 365, "week/year table");

        // ---------- TimestampDelta: factories / constants ----------
        Check(Near(TimestampDelta.FromMinutes(2).Seconds, 120), "FromMinutes");
        Check(Near(TimestampDelta.FromHours(1).Seconds, 3600), "FromHours");
        Check(Near(TimestampDelta.FromDays(1).Seconds, 86400), "FromDays");
        Check(TimestampDelta.OneDay.Seconds == 86400 && TimestampDelta.OneWeek.Seconds == 604800, "named delta constants");
        Check(TimestampDelta.OneMinute.Seconds == 60 && TimestampDelta.OneHour.Seconds == 3600, "OneMinute/OneHour");

        // ---------- TimestampDelta: decomposition ----------
        var d = TimestampDelta.FromDays(1) + TimestampDelta.FromHours(2) + TimestampDelta.FromMinutes(3) + TimestampDelta.FromSeconds(4);
        Check(d.Days == 1 && d.Hours == 2 && d.Minutes == 3, "delta component decomposition");
        Check(d.TotalHours == 26 && d.TotalMinutes == 26 * 60 + 3, "delta total decomposition");
        Check(d.TotalDays == 1, "delta total days");

        // ---------- TimestampDelta: math / operators ----------
        Check(TimestampDelta.Max(TimestampDelta.OneHour, TimestampDelta.OneDay) == TimestampDelta.OneDay, "delta Max");
        Check(TimestampDelta.Min(TimestampDelta.OneHour, TimestampDelta.OneDay) == TimestampDelta.OneHour, "delta Min");
        Check(TimestampDelta.Clamp(TimestampDelta.FromDays(5), TimestampDelta.Zero, TimestampDelta.OneDay) == TimestampDelta.OneDay, "delta Clamp");
        Check((TimestampDelta.OneHour * 2).Seconds == 7200, "delta * int");
        Check((3L * TimestampDelta.OneHour).Seconds == 10800, "long * delta");
        Check(Near((TimestampDelta.OneDay / 2).Seconds, 43200), "delta / int");
        Check(Near(TimestampDelta.OneDay / TimestampDelta.OneHour, 24), "delta / delta ratio");
        Check((TimestampDelta.FromHours(25) % TimestampDelta.OneDay).Seconds == 3600, "delta % delta");
        Check((-TimestampDelta.OneHour).Seconds == -3600, "delta unary negate");
        Check(TimestampDelta.TryParse("90", out var dp) && dp.Seconds == 90, "delta TryParse");
        Check(!TimestampDelta.TryParse("x", out _), "delta TryParse fails on garbage");

        // ---------- Timestamp: math ----------
        var ta = Timestamp.FromUnixSeconds(1000);
        var tb = Timestamp.FromUnixSeconds(2000);
        Check(Timestamp.Lerp(ta, tb, 0.25).UnixSeconds == 1250, "Timestamp Lerp");
        Check(Near(Timestamp.Unlerp(ta, tb, Timestamp.FromUnixSeconds(1750)), 0.75), "Timestamp Unlerp");
        Check(Timestamp.Clamp(Timestamp.FromUnixSeconds(500), ta, tb) == ta, "Timestamp Clamp low");
        Check(Timestamp.Max(ta, tb) == tb && Timestamp.Min(ta, tb) == ta, "Timestamp Min/Max");

        // ---------- Timestamp: rounding ----------
        var frac = Timestamp.FromUnixSeconds(1000.75);
        Check(frac.FloorToSecond().UnixSeconds == 1000, "FloorToSecond");
        Check(frac.CeilToSecond().UnixSeconds == 1001, "CeilToSecond");
        Check(frac.RoundToSecond().UnixSeconds == 1001, "RoundToSecond");
        Check(frac.ToFloorRoundedMilliseconds() == 1000750, "ToFloorRoundedMilliseconds");
        Check(Timestamp.FromUnixSeconds(1000).GetDurationSinceEpoch().Seconds == 1000, "GetDurationSinceEpoch");
        Check(Timestamp.TryParse("1234.5", out var tp) && tp.UnixSeconds == 1234.5, "Timestamp TryParse");

        // ---------- Timestamp: period index ----------
        var weekStart = Timestamp.FromUnixSeconds(0);
        Check(weekStart.GetPeriodIndex(TimestampDelta.OneWeek) == 0, "period index at epoch");
        Check(Timestamp.FromUnixSeconds(CalendarConstants.SecondsInWeek * 3 + 10).GetPeriodIndex(TimestampDelta.OneWeek) == 3, "period index bucketing");
        bool periodThrew = false;
        try { weekStart.GetPeriodIndex(TimestampDelta.Zero); } catch (ArgumentOutOfRangeException) { periodThrew = true; }
        Check(periodThrew, "period index rejects non-positive period");

        // ---------- Timestamp: calendar day ----------
        var noonUtc = Timestamp.FromUtc(new DateTime(2026, 6, 25, 12, 0, 0, DateTimeKind.Utc));
        Check(noonUtc.GetStartOfDay() == Timestamp.FromUtc(new DateTime(2026, 6, 25, 0, 0, 0, DateTimeKind.Utc)), "GetStartOfDay UTC midnight");
        Check(noonUtc.GetStartOfNextDay() == Timestamp.FromUtc(new DateTime(2026, 6, 26, 0, 0, 0, DateTimeKind.Utc)), "GetStartOfNextDay");
        // ToScaledDayTime: real noon (half a real day) over a full-length day => half the day => 12h
        Check(Near(noonUtc.ToScaledDayTime(CalendarConstants.SecondsInDay).TotalHours, 12), "ToScaledDayTime full-length day");
        // compressed 900s day: real noon maps to (43200 * 96) % 86400 => 0
        Check(Near(noonUtc.ToScaledDayTime(900).TotalSeconds, 0), "ToScaledDayTime compressed day wraps");

        // ---------- EpochClock: periods ----------
        var pclock = new TestEpochClock(Timestamp.FromUnixSeconds(CalendarConstants.SecondsInDay * 10 + 500));
        Check(pclock.GetCurrentPeriodIndex(TimestampDelta.OneDay) == 10, "clock current period index");
        Check(pclock.GetCurrentPeriodIndex(TimestampDelta.OneDay, 7) == 3, "clock cyclic period index (10 % 7)");
        var day7 = Timestamp.FromUnixSeconds(CalendarConstants.SecondsInDay * 7);
        Check(pclock.GetPassedPeriod(TimestampDelta.OneDay, day7) == 3, "GetPassedPeriod");
        var day12 = Timestamp.FromUnixSeconds(CalendarConstants.SecondsInDay * 12);
        Check(pclock.GetRemainingPeriod(TimestampDelta.OneDay, day12) == 2, "GetRemainingPeriod");
        Check(pclock.GetPassedPeriodOrZero(TimestampDelta.OneDay, day12) == 0, "GetPassedPeriodOrZero clamps");
        Check(pclock.GetRemainingPeriodOrZero(TimestampDelta.OneDay, day7) == 0, "GetRemainingPeriodOrZero clamps");
        Check(pclock.GetDurationSinceEpoch().Seconds == CalendarConstants.SecondsInDay * 10 + 500, "clock GetDurationSinceEpoch");

        // ---------- EpochClock: passed/remaining duration ----------
        var dclock = new TestEpochClock(Timestamp.FromUnixSeconds(1000));
        Check(dclock.GetPassedDuration(Timestamp.FromUnixSeconds(900)).Seconds == 100, "GetPassedDuration signed");
        Check(dclock.GetPassedDuration(Timestamp.FromUnixSeconds(1100)).Seconds == -100, "GetPassedDuration negative in future");
        Check(dclock.GetPassedDurationOrZero(Timestamp.FromUnixSeconds(1100)) == TimestampDelta.Zero, "GetPassedDurationOrZero clamps");
        Check(dclock.GetRemainingDuration(Timestamp.FromUnixSeconds(900)).Seconds == -100, "GetRemainingDuration signed");

        // ---------- EpochClock: start/end time ----------
        Check(dclock.CalculateEndTime(TimestampDelta.OneHour).UnixSeconds == 1000 + 3600, "CalculateEndTime(delta)");
        Check(dclock.CalculateStartTime(TimestampDelta.OneHour).UnixSeconds == 1000 - 3600, "CalculateStartTime(delta)");
        var fclock = new TestEpochClock(Timestamp.FromUnixSeconds(1000.4));
        Check(fclock.CalculateEndTimeRoundedUp(TimestampDelta.FromSeconds(0.3)).UnixSeconds == 1001, "CalculateEndTimeRoundedUp");
        Check(fclock.CalculateStartTimeRoundedDown(TimestampDelta.FromSeconds(0.3)).UnixSeconds == 1000, "CalculateStartTimeRoundedDown");

        // ---------- EpochClock: progress ----------
        dclock.SetNow(Timestamp.FromUnixSeconds(1200));
        Check(Near(dclock.GetCurrentProgressUnClamped(Timestamp.FromUnixSeconds(1000), Timestamp.FromUnixSeconds(1100)), 2f), "progress unclamped exceeds 1");
        Check(Near(dclock.GetCurrentProgressClamped(Timestamp.FromUnixSeconds(1100), TimestampDelta.FromSeconds(100)), 1f), "progress by end+duration clamped");
        dclock.SetNow(Timestamp.FromUnixSeconds(1050));
        Check(Near(dclock.GetCurrentProgressUnClamped(Timestamp.FromUnixSeconds(1100), TimestampDelta.FromSeconds(100)), 0.5f), "progress by end+duration unclamped");

        // ---------- EpochClock: weekly reset boundary ----------
        // 2026-06-25 is a Thursday; next Monday start should be 2026-06-29 00:00 UTC
        var wclock = new TestEpochClock(Timestamp.FromUtc(new DateTime(2026, 6, 25, 15, 0, 0, DateTimeKind.Utc)));
        Check(wclock.GetStartOfTargetDay(DayOfWeek.Monday) == Timestamp.FromUtc(new DateTime(2026, 6, 29, 0, 0, 0, DateTimeKind.Utc)), "GetStartOfTargetDay next Monday");
        // same weekday => a week ahead (2026-07-02)
        Check(wclock.GetStartOfTargetDay(DayOfWeek.Thursday) == Timestamp.FromUtc(new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc)), "GetStartOfTargetDay same weekday jumps a week");

        // ---------- EpochClock: drift ----------
        var driftClock = new TestEpochClock(Timestamp.FromUnixSeconds(1000)); // Now == TickIndependentNow
        Check(driftClock.GetDriftError() == TimestampDelta.Zero, "no drift when aligned");
        Check(driftClock.IsDriftWithinTolerance() && driftClock.CheckDriftWithinTolerance(), "drift within tolerance");
        Check(dclock.GetCurrentProgressUnClamped(Timestamp.FromUnixSeconds(1000), Timestamp.FromUnixSeconds(1000)) >= 0, "zero-span progress does not divide by zero");

        Console.WriteLine("--------------------------------------------------");
        Console.WriteLine($"PFound.EpochClock: passed={s_passed} failed={s_failed}");
        return s_failed == 0 ? 0 : 1;
    }
}
