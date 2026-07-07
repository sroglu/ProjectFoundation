# PFound.Collections

Two allocation-conscious, engine-free data structures for hot paths: a double-ended
`PriorityQueue<TKey, TValue>` and a value-type `StructList<T>`.

## Quick reference

```csharp
using PFound.Collections;

var open = new PriorityQueue<float, Node>();     // A* frontier, cheapest-first
open.Add(node.FCost, node);
Node next = open.PopMin();

var bodies = new StructList<Body>();
ref Body b = ref bodies.AddWithoutInitializing();   // fill the slot in place, no copy
b.Position = spawn;
foreach (ref var body in bodies.AsSpan()) body.Position += body.Velocity * dt;
```

## Dependencies

None — engine-free, `autoReferenced: false`. Add `PFound.Collections` to your asmdef `references`.

## Docs

Deep reference: [MODULE.md](MODULE.md).
