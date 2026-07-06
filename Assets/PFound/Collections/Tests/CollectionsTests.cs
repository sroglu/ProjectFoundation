using System;
using System.Collections.Generic;
using System.Linq;
using PFound.Collections;

// Standalone mono/csc runner (pure C#) — parity oracle for PriorityQueue + StructList.
internal static class CollectionsTests
{
    private static int s_passed, s_failed;
    private static void Check(bool cond, string name)
    {
        if (cond) s_passed++;
        else { s_failed++; Console.WriteLine("  FAIL: " + name); }
    }

    private struct Point { public int X, Y; }

    public static int Main()
    {
        PriorityQueueBasics();
        PriorityQueueRandomizedAgainstSortedOracle();
        StructListTests();

        Console.WriteLine("--------------------------------------------------");
        Console.WriteLine($"PFound.Collections: passed={s_passed} failed={s_failed}");
        return s_failed == 0 ? 0 : 1;
    }

    private static void PriorityQueueBasics()
    {
        var pq = new PriorityQueue<int, string>();
        pq.Add(5, "five"); pq.Add(1, "one"); pq.Add(9, "nine"); pq.Add(3, "three");
        Check(pq.Count == 4, "PQ count");
        Check(pq.Min == "one" && pq.Max == "nine", "PQ Min/Max peek");
        Check(pq.PopMin() == "one" && pq.PopMax() == "nine", "PQ PopMin/PopMax");
        Check(pq.Count == 2 && pq.Min == "three" && pq.Max == "five", "PQ state after pops");
        pq.Clear();
        Check(pq.Count == 0, "PQ Clear");
        bool threw = false;
        try { pq.PopMin(); } catch (InvalidOperationException) { threw = true; }
        Check(threw, "PQ PopMin on empty throws");
    }

    private static void PriorityQueueRandomizedAgainstSortedOracle()
    {
        var rng = new Random(20260625);

        // Pop-all-min yields ascending; pop-all-max yields descending.
        bool ascOk = true, descOk = true;
        for (int trial = 0; trial < 5; trial++)
        {
            var a = new PriorityQueue<int, int>();
            var b = new PriorityQueue<int, int>();
            int n = 200 + rng.Next(300);
            for (int i = 0; i < n; i++) { int v = rng.Next(10000); a.Add(v, v); b.Add(v, v); }
            int prevMin = int.MinValue;
            for (int i = 0; i < n; i++) { int v = a.PopMin(); if (v < prevMin) ascOk = false; prevMin = v; }
            int prevMax = int.MaxValue;
            for (int i = 0; i < n; i++) { int v = b.PopMax(); if (v > prevMax) descOk = false; prevMax = v; }
        }
        Check(ascOk, "PQ PopMin drains in ascending order");
        Check(descOk, "PQ PopMax drains in descending order");

        // Interleaved Add/PopMin/PopMax vs a reference multiset; Min/Max always match.
        var pq = new PriorityQueue<int, int>();
        var reference = new List<int>();
        bool ok = true;
        for (int step = 0; step < 4000 && ok; step++)
        {
            int op = rng.Next(3);
            if (reference.Count == 0 || op == 0)
            {
                int v = rng.Next(1000);
                pq.Add(v, v); reference.Add(v);
            }
            else if (op == 1)
            {
                int expected = reference.Min();
                int got = pq.PopMin();
                if (got != expected) ok = false; else reference.Remove(expected);
            }
            else
            {
                int expected = reference.Max();
                int got = pq.PopMax();
                if (got != expected) ok = false; else reference.Remove(expected);
            }

            if (ok && reference.Count > 0 && (pq.Min != reference.Min() || pq.Max != reference.Max()))
                ok = false;
            if (pq.Count != reference.Count) ok = false;
        }
        Check(ok, "PQ interleaved ops match sorted oracle (min-max heap correctness)");
    }

    private static void StructListTests()
    {
        var list = new StructList<int>(2);
        list.Add(10); list.Add(20); list.Add(30); // forces a grow past capacity 2
        Check(list.Count == 3 && list.Capacity >= 3, "StructList add + grow");
        Check(list[0] == 10 && list[2] == 30, "StructList indexer read");

        // ref indexer mutates in place
        list[1] = 99;
        Check(list[1] == 99, "StructList ref indexer assignment");

        // AddWithoutInitializing returns a writable ref slot
        ref int slot = ref list.AddWithoutInitializing();
        slot = 77;
        Check(list.Count == 4 && list[3] == 77, "StructList AddWithoutInitializing");

        // AsSpan reflects + mutates through
        var span = list.AsSpan();
        Check(span.Length == 4 && span[0] == 10, "StructList AsSpan view");
        span[0] = -1;
        Check(list[0] == -1, "StructList AsSpan mutation writes through");

        // RemoveAtSwapBack: last moves into the removed slot
        list.RemoveAtSwapBack(0); // removes -1; 77 (last) moves to index 0
        Check(list.Count == 3 && list[0] == 77, "StructList RemoveAtSwapBack moves last into place");

        // ref mutation of a struct element field
        var pts = new StructList<Point>();
        pts.Add(new Point { X = 1, Y = 2 });
        pts[0].X = 42;
        Check(pts[0].X == 42 && pts[0].Y == 2, "StructList in-place struct field mutation via ref");

        list.Clear();
        Check(list.Count == 0, "StructList Clear");
    }
}
