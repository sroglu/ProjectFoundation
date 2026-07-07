# Collections

## Purpose

Two allocation-conscious, engine-free data structures for hot paths: a double-ended
`PriorityQueue<TKey, TValue>` (min-max heap) and a value-type `StructList<T>`. They exist so
pathfinding frontiers, per-frame struct buffers, and similar tight loops avoid the GC pressure and
copy overhead of `List<T>` / `SortedList<T>`.

## Assemblies

- `PFound.Collections` (runtime) — the two structures. `noEngineReferences: true`,
  `autoReferenced: false` (a consumer must add it to its asmdef `references` explicitly).
- `PFound.Collections.Tests` — NUnit suite.

## Dependencies

None. No Unity references, no PFound modules, no third-party packages, no scripting defines.

## Key Types

`PFound.Collections` namespace:

- `PriorityQueue<TKey, TValue>` — min-max heap; both ends reachable in O(log n).
- `StructList<T>` — growable, array-backed list tuned for `struct` element types.

## Public API

**`PriorityQueue<TKey, TValue>`** — ordering is by `TKey` via an `IComparer<TKey>`.

- `new PriorityQueue<TKey, TValue>(int capacity = 8, IComparer<TKey> comparer = null)` — `comparer`
  defaults to `Comparer<TKey>.Default`.
- `void Add(TKey key, TValue value)` — O(log n).
- `TValue PopMin()` / `TValue PopMax()` — O(log n) from either end.
- `TValue Min` / `TValue Max` — O(1) peek.
- `int Count`, `void Clear()`.
- Peeking or popping an empty queue throws `InvalidOperationException` — fail-fast, no sentinel.

**`StructList<T>`** — one backing array, no per-element boxing.

- `new StructList<T>(int capacity = 4)`.
- `ref T this[int index]` — in-place read/mutate without copying the struct.
- `void Add(in T item)`.
- `ref T AddWithoutInitializing()` — claims the next slot and returns it by `ref` without clearing
  (caller fills it).
- `void RemoveAtSwapBack(int index)` — O(1) removal, order not preserved.
- `Span<T> AsSpan()` — zero-copy view over `[0, Count)`; invalidated by any add/remove.
- `int Count`, `int Capacity`, `void Clear()`.
- Out-of-range index access throws `ArgumentOutOfRangeException`.

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
```

Main-thread only — neither structure is thread-safe.

## File Structure

```
Collections/
  Runtime/
    PriorityQueue.cs                  # min-max heap
    StructList.cs                     # array-backed struct list
    PFound.Collections.asmdef         # engine-free, autoReferenced:false
  Tests/
    CollectionsTests.cs               # heap ordering, span mutation, swap-back removal
    PFound.Collections.Tests.asmdef
```

## Downstream Dependents

None within PFound. Intended for consumers writing hot-path game code (pathfinding, particle/physics
buffers) that reference the assembly directly.

## Limitations / Known Gaps

- Main-thread only; no internal synchronization.
- `AsSpan()` and `ref` accessors are invalidated by any structural change (add/remove/clear) — treat
  the span as a per-loop borrow, never store it across a mutation.
- `PriorityQueue` is not stable: equal keys have unspecified relative pop order.
- `RemoveAtSwapBack` does not preserve insertion order (that is the trade for O(1) removal).

## Testing

`Tests/CollectionsTests.cs` (assembly `PFound.Collections.Tests`) covers heap ordering, span
mutation, and swap-back removal. Being engine-free, the runtime also compiles and runs under a plain
`csc`/`mono` runner.
