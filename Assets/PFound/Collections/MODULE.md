# Collections

## Purpose

Allocation-conscious, engine-free data structures and graph search for hot paths: a double-ended
`PriorityQueue<TKey, TValue>` (min-max heap), a value-type `StructList<T>`, and a generic
best-first / A* `PathFinder` over a caller-supplied graph (with a ready-made 2D `GridBoard`). They
exist so pathfinding frontiers, per-frame struct buffers, and similar tight loops avoid the GC
pressure and copy overhead of `List<T>` / `SortedList<T>`.

## Assemblies

- `PFound.Collections` (runtime) — the structures + graph search. `noEngineReferences: true`,
  `autoReferenced: false` (a consumer must add it to its asmdef `references` explicitly).
- `PFound.Collections.Tests` — NUnit suite.

## Dependencies

None. No Unity references, no PFound modules, no third-party packages, no scripting defines.

## Key Types

`PFound.Collections` namespace:

- `PriorityQueue<TKey, TValue>` — min-max heap; both ends reachable in O(log n).
- `StructList<T>` — growable, array-backed list tuned for `struct` element types.
- `INeighbourProvider<TNode>` — the graph abstraction a search expands (fills a caller-owned list of
  a node's reachable neighbours; keeps expansion allocation-free).
- `GridCoord` — an integer 2D cell coordinate (`readonly struct`, `IEquatable`).
- `GridBoard` — a rectangular tile board implementing `INeighbourProvider<GridCoord>`; 4- or
  8-connectivity, optional passability predicate, diagonal corner-cutting blocked.
- `GridHeuristics` — Manhattan / Chebyshev / Euclidean distance estimators for step cost + heuristic.
- `Path<TNode>` — an immutable start→node step chain (shares its prefix; enumerates in travel order).
- `PathFinder` — static generic best-first / A* search returning the lowest-cost `Path<TNode>`.

## Public API

**`PriorityQueue<TKey, TValue>`** — ordering is by `TKey` via an `IComparer<TKey>`.

- `new PriorityQueue<TKey, TValue>(int capacity = 8, IComparer<TKey> comparer = null)` — `comparer`
  defaults to `Comparer<TKey>.Default`.
- `void Add(TKey key, TValue value)` — O(log n).
- `TValue PopMin()` / `TValue PopMax()` — O(log n) from either end.
- `TValue Min` / `TValue Max` — O(1) peek.
- `bool Remove(TValue value)` — removes the first entry whose value matches (by
  `EqualityComparer<TValue>.Default`) and re-heapifies; O(n). Returns whether one was found.
- `bool IsEmpty`, `int Count`, `void Clear()`.
- `Enumerator GetEnumerator()` — allocation-free `foreach` over the contained values in heap
  (not priority) order.
- Peeking or popping an empty queue throws `InvalidOperationException` — fail-fast, no sentinel.

**`StructList<T>`** — one backing array, no per-element boxing.

- `new StructList<T>(int capacity = 4)`.
- `ref T this[int index]` — in-place read/mutate without copying the struct.
- `void Add(in T item)`.
- `void AddRange(ReadOnlySpan<T> items)` — bulk-append every element in order.
- `void Insert(int index, in T item)` — order-preserving insert, shifting later elements right.
- `ref T AddWithoutInitializing()` — claims the next slot and returns it by `ref` without clearing
  (caller fills it).
- `void RemoveAt(int index)` — O(n) order-preserving removal (shifts later elements left).
- `void RemoveAtSwapBack(int index)` — O(1) removal, order not preserved.
- `Span<T> AsSpan()` / `Span<T> AsSpan(int start, int length)` — zero-copy writable view over the
  live range or a sub-range; invalidated by any add/remove.
- `ReadOnlySpan<T> AsReadOnlySpan()` / `AsReadOnlySpan(int start, int length)` — read-only spans.
- `T[] ToArray()` — copies `[0, Count)` into a fresh array.
- `T[] GetInternalArray()` — the backing array (length == `Capacity`); zero-copy interop, mutate with care.
- `Enumerator GetEnumerator()` — allocation-free `foreach`; `Current` is a `ref` for in-place edit.
- `int Count`, `int Capacity`, `void Clear()`.
- Out-of-range index access throws `ArgumentOutOfRangeException`.

**Graph & pathfinding**

- `interface INeighbourProvider<TNode> { void GetNeighbours(TNode node, List<TNode> results); }` —
  implement this to describe any graph; the search fills `results` with `node`'s reachable neighbours.
- `readonly struct GridCoord { int X; int Y; GridCoord(int x, int y); }` — `IEquatable<GridCoord>`.
- `GridBoard : INeighbourProvider<GridCoord>`
  - `new GridBoard(int width, int height, bool allowDiagonal = false, Predicate<GridCoord> isPassable = null)`
    — `isPassable == null` ⇒ every in-bounds cell passable; positive `width`/`height` required.
  - `int Width`, `int Height`, `bool AllowDiagonal`.
  - `bool InBounds(GridCoord cell)`, `bool IsPassable(GridCoord cell)`.
  - `void GetNeighbours(GridCoord node, List<GridCoord> results)` — orthogonal (and, if enabled,
    diagonal) passable neighbours; diagonal moves that would cut a corner between two blocked
    orthogonal cells are excluded.
- `static class GridHeuristics` — `float Manhattan(a, b)`, `float Chebyshev(a, b)`,
  `float Euclidean(a, b)` (each `(GridCoord, GridCoord) → float`).
- `sealed class Path<TNode> : IEnumerable<TNode>`
  - `static Path<TNode> Start(TNode node)` — single-node path, zero cost.
  - `Path<TNode> Append(TNode node, float stepCost)` — new tail sharing this prefix (immutable).
  - `TNode Node`, `Path<TNode> Previous`, `float Cost`, `int Length`; enumerates start→end.
- `static class PathFinder`
  - `Path<TNode> FindPath<TNode>(TNode start, TNode goal, INeighbourProvider<TNode> graph,
    Func<TNode,TNode,float> stepCost, Func<TNode,TNode,float> heuristic,
    IEqualityComparer<TNode> comparer = null)` — lowest-cost path start→goal, or `null` if
    unreachable. A heuristic of constant 0 degenerates to Dijkstra / uniform-cost search.

## Setup / wiring

Pure library — `new PriorityQueue<TKey, TValue>()` / `new StructList<T>()`. No scene object, no
MonoBehaviour host, no lifecycle, no ScriptableObject, no DI registration. Because the assembly is
`autoReferenced: false`, a consumer assembly must add **`PFound.Collections`** to its asmdef
`references` before the types resolve.

```csharp
using PFound.Collections;

var open = new PriorityQueue<float, Node>();     // A* frontier, cheapest-first
open.Add(node.FCost, node);
Node next = open.PopMin();

var bodies = new StructList<Body>();             // per-frame struct buffer
ref Body b = ref bodies.AddWithoutInitializing();
b.Position = spawn;                              // written in place, no copy
foreach (ref var body in bodies.AsSpan()) body.Position += body.Velocity * dt;

// A* over a tile grid:
var board = new GridBoard(w, h, allowDiagonal: true, isPassable: c => !walls[c.X, c.Y]);
Path<GridCoord> route = PathFinder.FindPath(
    new GridCoord(0, 0), new GridCoord(9, 9), board,
    stepCost: (a, c) => GridHeuristics.Euclidean(a, c),
    heuristic: GridHeuristics.Chebyshev);        // null when unreachable
```

Main-thread only — none of these structures is thread-safe.

## File Structure

```
Collections/
  Runtime/
    PriorityQueue.cs                  # min-max heap
    StructList.cs                     # array-backed struct list
    Graph.cs                          # INeighbourProvider, GridCoord, GridBoard, GridHeuristics
    Path.cs                           # immutable Path<TNode> step chain
    PathFinder.cs                     # generic best-first / A* FindPath
    PFound.Collections.asmdef         # engine-free, autoReferenced:false
  Tests/
    CollectionsTests.cs               # heap ordering, span mutation, swap-back removal, pathfinding
    PFound.Collections.Tests.asmdef
```

## Downstream Dependents

None within PFound. Intended for consumers writing hot-path game code (pathfinding, particle/physics
buffers) that reference the assembly directly.

## Limitations / Known Gaps

- Main-thread only; no internal synchronization.
- `AsSpan()`, `AsReadOnlySpan()`, the enumerators, and `ref` accessors are invalidated by any
  structural change (add/remove/clear) — treat them as a per-loop borrow, never store across a mutation.
- `GetInternalArray()` exposes the raw backing array (length == `Capacity`, not `Count`) and is
  reallocated on growth — for zero-copy interop only; never cache it across an add.
- `PriorityQueue` is not stable: equal keys have unspecified relative pop order, and both `Remove`
  and the enumerator visit values in heap order, not priority order. `Remove` is O(n).
- `RemoveAtSwapBack` does not preserve insertion order (that is the trade for O(1) removal).
- `PathFinder` returns an optimal path only for an admissible heuristic (`GridHeuristics.*` are
  admissible for their matching connectivity); an over-estimating heuristic trades optimality for
  speed. `PathFinder` / `Path<TNode>` allocate their working dictionaries/sets and the result chain
  (not part of the zero-alloc `StructList`/`PriorityQueue` guarantee); `GridBoard.GetNeighbours`
  itself is allocation-free (it fills a caller-owned list).

## Testing

`Tests/CollectionsTests.cs` (assembly `PFound.Collections.Tests`) covers heap ordering + interior
`Remove`, span/enumerator mutation, order-preserving vs swap-back removal, and grid A* pathfinding.
Being engine-free, the runtime also compiles and runs under a plain `csc`/`mono` runner.
