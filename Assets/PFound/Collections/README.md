# PFound.Collections

Two allocation-conscious, engine-free data structures for hot paths: a double-ended
`PriorityQueue<TKey, TValue>` and a value-type `StructList<T>`. No Unity references, no
third-party deps — `main-thread only`, `new()`-able, unit-tested without Unity.

## Public API

**`PriorityQueue<TKey, TValue>`** — min-max heap; O(log n) push/pop from either end, O(1) peeks.
- `new PriorityQueue<TKey, TValue>(int capacity = 8, IComparer<TKey> comparer = null)` — ordering
  is by `TKey` via the comparer (defaults to `Comparer<TKey>.Default`).
- `Add(TKey key, TValue value)`, `PopMin()`, `PopMax()`.
- `Min` / `Max` (peek), `Count`, `Clear()`.
- Peeking or popping an empty queue throws `InvalidOperationException` — fail-fast, no sentinel.

**`StructList<T>`** — growable list tuned for structs, backed by one array.
- `new StructList<T>(int capacity = 4)`.
- `ref T this[int index]` — in-place read/mutate without copying the struct.
- `Add(in T item)`, `AddWithoutInitializing()` — claims the next slot and returns it by `ref`
  without clearing (caller fills it).
- `RemoveAtSwapBack(int index)` — O(1) removal, order not preserved.
- `Span<T> AsSpan()` — zero-copy view over `[0, Count)`; invalidated by any add/remove.
- `Count`, `Capacity`, `Clear()`.
- Out-of-range index access throws `ArgumentOutOfRangeException`.

## Setup / wiring

Pure library — `new PriorityQueue<TKey, TValue>()` / `new StructList<T>()`. No scene object, no
MonoBehaviour host, no lifecycle, no ScriptableObject, no DI registration. The assembly
(`PFound.Collections`, namespace `PFound.Collections`) is engine-free and `autoReferenced:false`,
so a consumer assembly must add **`PFound.Collections`** to its asmdef `references` to use it.

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

## Testing

`Tests/CollectionsTests.cs` (assembly `PFound.Collections.Tests`) covers heap ordering, span
mutation, and swap-back removal. Being engine-free, the runtime also compiles and runs under a
plain `csc`/`mono` runner.

## Layout

- `Runtime/` — `PriorityQueue.cs`, `StructList.cs`. Assembly `PFound.Collections`.
- `Tests/` — NUnit suite.
