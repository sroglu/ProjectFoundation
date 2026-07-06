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

        Console.WriteLine("--------------------------------------------------");
        Console.WriteLine($"PFound.EpochClock: passed={s_passed} failed={s_failed}");
        return s_failed == 0 ? 0 : 1;
    }
}
