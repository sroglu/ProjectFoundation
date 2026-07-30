# PFound.Collections

Allocation-conscious, engine-free data structures and graph search for hot paths: a double-ended
`PriorityQueue<TKey, TValue>`, a value-type `StructList<T>`, and a generic best-first / A*
`PathFinder` (with a ready-made 2D `GridBoard`).

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

// A* over a tile grid (or any INeighbourProvider<TNode>):
var board = new GridBoard(w, h, allowDiagonal: true, isPassable: c => !walls[c.X, c.Y]);
Path<GridCoord> route = PathFinder.FindPath(
    new GridCoord(0, 0), new GridCoord(9, 9), board,
    stepCost: (a, b2) => GridHeuristics.Euclidean(a, b2),
    heuristic: GridHeuristics.Chebyshev);           // null when unreachable
```

## Dependencies

None — engine-free, `autoReferenced: false`. Add `PFound.Collections` to your asmdef `references`.

## Docs

Deep reference: [MODULE.md](MODULE.md).
