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
        PriorityQueueRemoveAndEnumeration();
        StructListTests();
        StructListParityAdditions();
        GridBoardTests();
        PathTests();
        PathFinderTests();

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

    private static void PriorityQueueRemoveAndEnumeration()
    {
        var pq = new PriorityQueue<int, string>();
        Check(pq.IsEmpty, "PQ IsEmpty when new");
        pq.Add(5, "five"); pq.Add(1, "one"); pq.Add(9, "nine"); pq.Add(3, "three");
        Check(!pq.IsEmpty, "PQ not IsEmpty after adds");

        // Enumeration yields every contained value (order unspecified).
        var seen = new List<string>();
        foreach (var v in pq) seen.Add(v);
        Check(seen.Count == 4 && seen.Contains("one") && seen.Contains("nine")
              && seen.Contains("five") && seen.Contains("three"), "PQ enumeration yields all values");

        // Remove an interior value; heap invariants (Min/Max) still hold.
        Check(pq.Remove("five"), "PQ Remove returns true for present value");
        Check(!pq.Remove("absent"), "PQ Remove returns false for missing value");
        Check(pq.Count == 3 && pq.Min == "one" && pq.Max == "nine", "PQ state after interior Remove");
        Check(pq.Remove("nine") && pq.Max == "three", "PQ Remove of current Max updates Max");
        Check(pq.Remove("one") && pq.Min == "three", "PQ Remove of current Min updates Min");
        Check(pq.Count == 1 && pq.Remove("three") && pq.IsEmpty, "PQ Remove drains to empty");

        // Randomized Remove keeps heap correct vs a reference multiset.
        var rng = new Random(20260708);
        bool ok = true;
        for (int trial = 0; trial < 40 && ok; trial++)
        {
            var heap = new PriorityQueue<int, int>();
            var reference = new List<int>();
            int n = 30 + rng.Next(40);
            for (int i = 0; i < n; i++) { int v = rng.Next(500); heap.Add(v, v); reference.Add(v); }
            // Remove a random subset.
            for (int r = 0; r < n / 2 && reference.Count > 0; r++)
            {
                int victim = reference[rng.Next(reference.Count)];
                if (!heap.Remove(victim)) { ok = false; break; }
                reference.Remove(victim);
                if (reference.Count > 0 && (heap.Min != reference.Min() || heap.Max != reference.Max())) { ok = false; break; }
                if (heap.Count != reference.Count) { ok = false; break; }
            }
            // Drain-min must be ascending after arbitrary removals.
            int prev = int.MinValue;
            while (!heap.IsEmpty && ok) { int v = heap.PopMin(); if (v < prev) ok = false; prev = v; }
        }
        Check(ok, "PQ Remove keeps min-max heap correct (randomized vs oracle)");
    }

    private static void StructListParityAdditions()
    {
        var list = new StructList<int>(2);

        // AddRange from a span.
        Span<int> src = stackalloc int[] { 1, 2, 3, 4 };
        list.AddRange(src);
        Check(list.Count == 4 && list[0] == 1 && list[3] == 4, "StructList AddRange(ReadOnlySpan)");

        // Insert shifts right (order preserved).
        list.Insert(0, 99);   // 99,1,2,3,4
        list.Insert(2, 88);   // 99,1,88,2,3,4
        Check(list.Count == 6 && list[0] == 99 && list[2] == 88 && list[5] == 4, "StructList Insert shifts right");

        // Order-preserving RemoveAt shifts left.
        list.RemoveAt(0);     // 1,88,2,3,4
        list.RemoveAt(1);     // 1,2,3,4
        Check(list.Count == 4 && list[0] == 1 && list[1] == 2 && list[3] == 4, "StructList order-preserving RemoveAt");

        // ToArray snapshots current live elements.
        var arr = list.ToArray();
        Check(arr.Length == 4 && arr[0] == 1 && arr[3] == 4, "StructList ToArray");

        // AsReadOnlySpan / sub-spans.
        var ro = list.AsReadOnlySpan();
        Check(ro.Length == 4 && ro[2] == 3, "StructList AsReadOnlySpan");
        var roSub = list.AsReadOnlySpan(1, 2);
        Check(roSub.Length == 2 && roSub[0] == 2 && roSub[1] == 3, "StructList AsReadOnlySpan(start,len)");
        var sub = list.AsSpan(1, 2);
        sub[0] = 200;
        Check(list[1] == 200, "StructList AsSpan(start,len) writes through");

        // GetInternalArray exposes backing store (length == capacity).
        var backing = list.GetInternalArray();
        Check(backing.Length == list.Capacity && backing.Length >= list.Count, "StructList GetInternalArray is the backing array");

        // foreach via ref enumerator (mutation writes through).
        int sum = 0;
        foreach (var x in list) sum += x;
        Check(sum == 1 + 200 + 3 + 4, "StructList GetEnumerator (foreach)");
        foreach (ref int x in list) x = 0;
        Check(list[0] == 0 && list[3] == 0, "StructList foreach ref mutation writes through");

        // Range validation is fail-fast.
        bool threw = false;
        try { list.AsSpan(3, 5); } catch (ArgumentException) { threw = true; }
        Check(threw, "StructList AsSpan out-of-range throws");
    }

    private static void GridBoardTests()
    {
        // 4-connectivity in the interior yields 4 neighbours; corners yield 2.
        var open = new GridBoard(5, 5);
        var buf = new List<GridCoord>();
        open.GetNeighbours(new GridCoord(2, 2), buf);
        Check(buf.Count == 4, "GridBoard 4-connectivity interior neighbour count");
        buf.Clear();
        open.GetNeighbours(new GridCoord(0, 0), buf);
        Check(buf.Count == 2, "GridBoard corner neighbour count (bounds clamp)");

        // Passability predicate blocks a cell.
        var blocked = new GridBoard(5, 5, allowDiagonal: false, isPassable: c => c.X != 1);
        buf.Clear();
        blocked.GetNeighbours(new GridCoord(0, 2), buf);
        Check(!buf.Contains(new GridCoord(1, 2)), "GridBoard passability predicate blocks cell");

        // Diagonal mode: 8 neighbours interior; no corner-cutting through two blocked orthogonals.
        var diag = new GridBoard(5, 5, allowDiagonal: true);
        buf.Clear();
        diag.GetNeighbours(new GridCoord(2, 2), buf);
        Check(buf.Count == 8, "GridBoard 8-connectivity interior neighbour count");

        var pinched = new GridBoard(5, 5, allowDiagonal: true,
            isPassable: c => !(c.X == 3 && c.Y == 2) && !(c.X == 2 && c.Y == 3));
        buf.Clear();
        pinched.GetNeighbours(new GridCoord(2, 2), buf);
        Check(!buf.Contains(new GridCoord(3, 3)), "GridBoard no diagonal corner-cut between two blocked cells");
    }

    private static void PathTests()
    {
        var p = Path<int>.Start(1).Append(2, 1.5f).Append(3, 2.0f);
        Check(p.Length == 3 && p.Node == 3, "Path length + head node");
        Check(Math.Abs(p.Cost - 3.5f) < 1e-4f, "Path accumulates cost");

        var order = p.ToList();  // IEnumerable start -> end
        Check(order.Count == 3 && order[0] == 1 && order[1] == 2 && order[2] == 3, "Path enumerates start -> end");

        // Immutability: appending does not mutate the shared prefix.
        var prefix = Path<int>.Start(10);
        var a = prefix.Append(20, 1f);
        var b = prefix.Append(30, 5f);
        Check(prefix.Length == 1 && a.Node == 20 && b.Node == 30 && a.Previous == prefix && b.Previous == prefix,
            "Path is immutable (prefix shared, not mutated)");
    }

    private static void PathFinderTests()
    {
        // Open 5x5 grid, 4-connectivity, unit step cost, Manhattan heuristic.
        var board = new GridBoard(5, 5);
        var path = PathFinder.FindPath(
            new GridCoord(0, 0), new GridCoord(4, 4), board,
            (a, b) => 1f, GridHeuristics.Manhattan);
        Check(path != null, "PathFinder finds a path on open grid");
        Check(path.Node.Equals(new GridCoord(4, 4)) && path.ToList()[0].Equals(new GridCoord(0, 0)),
            "PathFinder path runs start -> goal");
        Check(Math.Abs(path.Cost - 8f) < 1e-4f && path.Length == 9,
            "PathFinder returns optimal length on open grid (8 steps)");

        // Wall across the middle with a single gap forces a detour.
        var walled = new GridBoard(5, 5, allowDiagonal: false,
            isPassable: c => !(c.Y == 2 && c.X != 4));
        var detour = PathFinder.FindPath(
            new GridCoord(0, 0), new GridCoord(0, 4), walled,
            (a, b) => 1f, GridHeuristics.Manhattan);
        Check(detour != null && detour.Cost > 4f, "PathFinder detours around a wall");
        // Every node on the returned path must be passable.
        bool allPassable = true;
        foreach (var c in detour) if (!walled.IsPassable(c)) allPassable = false;
        Check(allPassable, "PathFinder path only crosses passable cells");

        // Fully sealed goal -> unreachable -> null.
        var sealedBoard = new GridBoard(5, 5, allowDiagonal: false,
            isPassable: c => !(c.X == 3));  // full vertical wall, no gap
        var none = PathFinder.FindPath(
            new GridCoord(0, 0), new GridCoord(4, 4), sealedBoard,
            (a, b) => 1f, GridHeuristics.Manhattan);
        Check(none == null, "PathFinder returns null when goal is unreachable");

        // Zero heuristic (Dijkstra) still returns an optimal-cost path.
        var dijkstra = PathFinder.FindPath(
            new GridCoord(0, 0), new GridCoord(3, 0), board,
            (a, b) => 1f, (a, b) => 0f);
        Check(dijkstra != null && Math.Abs(dijkstra.Cost - 3f) < 1e-4f, "PathFinder with zero heuristic (Dijkstra) is optimal");
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
