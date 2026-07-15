using System;
using System.Collections.Generic;
using System.Linq;
using PFound.Utilities.DataType;

// Standalone mono/csc runner (pure C#, no NUnit) — behavior oracle for DataTypes.
// Exit code 0 = all pass.
internal static class DataTypesTests
{
    private static int s_passed, s_failed;

    private static void Check(bool cond, string name)
    {
        if (cond) s_passed++;
        else { s_failed++; Console.WriteLine("  FAIL: " + name); }
    }

    public static int Main()
    {
        CircularArrayTests();
        MinMaxRangeTests();
        KeyValueTests();
        UnixTimeTests();

        Console.WriteLine($"DataTypes: {s_passed} passed, {s_failed} failed (total {s_passed + s_failed})");
        return s_failed == 0 ? 0 : 1;
    }

    private static void CircularArrayTests()
    {
        var buffer = new CircularArray<int>(3);
        Check(buffer.Capacity == 3 && buffer.Count == 0 && buffer.IsEmpty, "Circular initial state");

        buffer.Append(1);
        buffer.Append(2);
        Check(buffer.Count == 2 && !buffer.IsFull, "Circular partial fill");
        Check(buffer[0] == 1 && buffer[1] == 2, "Circular oldest-to-newest indexing");

        buffer.Append(3);
        Check(buffer.IsFull && buffer.Oldest == 1 && buffer.Newest == 3, "Circular full oldest/newest");

        buffer.Append(4); // overwrites 1
        Check(buffer.Count == 3 && buffer.Oldest == 2 && buffer.Newest == 4, "Circular overwrite drops oldest");
        Check(buffer.SequenceEqual(new[] { 2, 3, 4 }), "Circular enumeration order after wrap");
        Check(!buffer.Contains(1) && buffer.Contains(3), "Circular Contains reflects retained set");

        var dest = new int[3];
        buffer.CopyTo(dest, 0);
        Check(dest[0] == 2 && dest[1] == 3 && dest[2] == 4, "Circular CopyTo");

        // ICollection.Add routes to Append
        ICollection<int> asCollection = new CircularArray<int>(2);
        asCollection.Add(9);
        Check(asCollection.Count == 1, "Circular ICollection.Add == Append");

        var removeThrew = false;
        try { asCollection.Remove(9); } catch (NotSupportedException) { removeThrew = true; }
        Check(removeThrew, "Circular Remove unsupported");

        buffer.Clear();
        Check(buffer.Count == 0 && buffer.IsEmpty, "Circular Clear");

        var ctorThrew = false;
        try { var _ = new CircularArray<int>(0); } catch (ArgumentOutOfRangeException) { ctorThrew = true; }
        Check(ctorThrew, "Circular rejects non-positive capacity");
    }

    private static void MinMaxRangeTests()
    {
        var r = new MinMaxRange(2f, 6f);
        Check(Math.Abs(r.Length - 4f) < 1e-6f, "MinMax Length");
        Check(Math.Abs(r.Center - 4f) < 1e-6f, "MinMax Center");
        Check(r.Contains(3f) && !r.Contains(7f), "MinMax Contains");
        Check(Math.Abs(r.Clamp(10f) - 6f) < 1e-6f && Math.Abs(r.Clamp(-1f) - 2f) < 1e-6f, "MinMax Clamp");
        Check(Math.Abs(r.Lerp(0.5f) - 4f) < 1e-6f, "MinMax Lerp");
        Check(Math.Abs(r.Lerp(-1f) - 2f) < 1e-6f && Math.Abs(r.Lerp(2f) - 6f) < 1e-6f, "MinMax Lerp clamps t");
        Check(Math.Abs(r.InverseLerp(4f) - 0.5f) < 1e-6f, "MinMax InverseLerp");

        Check(MinMaxRange.Zero == new MinMaxRange(0f, 0f), "MinMax Zero");
        Check(MinMaxRange.Identity == new MinMaxRange(0f, 1f), "MinMax Identity");
        Check(!MinMaxRange.Invalid.IsValid, "MinMax Invalid is not valid");

        var grow = MinMaxRange.Invalid;
        grow.Encapsulate(5f);
        grow.Encapsulate(-2f);
        Check(grow.IsValid && Math.Abs(grow.Min + 2f) < 1e-6f && Math.Abs(grow.Max - 5f) < 1e-6f, "MinMax Encapsulate from Invalid");

        var rng = new Random(12345);
        var sample = r.Random(rng);
        Check(sample >= 2f && sample <= 6f, "MinMax Random within bounds");

        Check(new MinMaxRange(1f, 2f) != new MinMaxRange(1f, 3f), "MinMax inequality");
    }

    private static void KeyValueTests()
    {
        var kv = new KeyValue<string, int>("hp", 100);
        Check(kv.Key == "hp" && kv.Value == 100, "KeyValue construct");

        kv.Value = 50; // mutable
        Check(kv.Value == 50, "KeyValue mutable Value");

        KeyValuePair<string, int> asPair = kv;
        Check(asPair.Key == "hp" && asPair.Value == 50, "KeyValue implicit -> KeyValuePair");

        KeyValue<string, int> back = new KeyValuePair<string, int>("mp", 20);
        Check(back.Key == "mp" && back.Value == 20, "KeyValue implicit <- KeyValuePair");

        Check(new KeyValue<int, int>(1, 2).Equals(new KeyValue<int, int>(1, 2)), "KeyValue equality");
        Check(!new KeyValue<int, int>(1, 2).Equals(new KeyValue<int, int>(1, 3)), "KeyValue inequality");
    }

    private static void UnixTimeTests()
    {
        // 2001-09-09 01:46:40 UTC == 1000000000 seconds
        var dt = new DateTime(2001, 9, 9, 1, 46, 40, DateTimeKind.Utc);
        Check(UnixTime.ToUnixTime(dt) == 1000000000L, "UnixTime ToUnixTime known value");
        Check(UnixTime.ConvertFrom(1000000000L) == dt, "UnixTime ConvertFrom roundtrip");
        Check(UnixTime.ConvertTo(dt) == 1000000000L, "UnixTime ConvertTo alias");

        Check(UnixTime.ToUnixTime(UnixTime.Epoch) == 0L, "UnixTime epoch is zero");

        var t = new UnixTime(1000000000L);
        Check(t.ToDateTime() == dt, "UnixTime instance ToDateTime");
        Check((long)t == 1000000000L, "UnixTime implicit to long");
        UnixTime fromLong = 42L;
        Check(fromLong.Seconds == 42L, "UnixTime implicit from long");

        Check(new UnixTime(5).CompareTo(new UnixTime(9)) < 0, "UnixTime CompareTo");
        Check(UnixTime.ToUnixTimeMilliseconds(dt) == 1000000000000L, "UnixTime milliseconds");

        var now = UnixTime.Now.Seconds;
        Check(now > 1000000000L, "UnixTime Now is after year 2001");
    }
}
