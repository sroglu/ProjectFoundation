using System;
using System.Collections.Generic;

namespace PFound.Collections
{
    /// <summary>
    /// Generic best-first / A* search over an <see cref="INeighbourProvider{TNode}"/>. The caller
    /// supplies the step cost between adjacent nodes and an (admissible) heuristic estimate to the
    /// goal; passing a heuristic that always returns 0 degenerates to Dijkstra / uniform-cost
    /// search. Returns the lowest-cost <see cref="Path{TNode}"/> from start to goal, or
    /// <c>null</c> when the goal is unreachable.
    /// </summary>
    public static class PathFinder
    {
        public static Path<TNode> FindPath<TNode>(
            TNode start,
            TNode goal,
            INeighbourProvider<TNode> graph,
            Func<TNode, TNode, float> stepCost,
            Func<TNode, TNode, float> heuristic,
            IEqualityComparer<TNode> comparer = null)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (stepCost == null) throw new ArgumentNullException(nameof(stepCost));
            if (heuristic == null) throw new ArgumentNullException(nameof(heuristic));
            comparer ??= EqualityComparer<TNode>.Default;

            // f-score-keyed frontier; cameFrom rebuilds the route; gScore holds best-known cost so far.
            var openSet = new PriorityQueue<float, TNode>(comparer: Comparer<float>.Default);
            var cameFrom = new Dictionary<TNode, TNode>(comparer);
            var gScore = new Dictionary<TNode, float>(comparer);
            var closed = new HashSet<TNode>(comparer);
            var neighbours = new List<TNode>();

            gScore[start] = 0f;
            openSet.Add(heuristic(start, goal), start);

            while (!openSet.IsEmpty)
            {
                TNode current = openSet.PopMin();
                if (comparer.Equals(current, goal))
                    return Reconstruct(current, start, cameFrom, stepCost, comparer);

                // Skip stale frontier entries left over from an earlier, worse relaxation.
                if (!closed.Add(current)) continue;

                float currentG = gScore[current];
                neighbours.Clear();
                graph.GetNeighbours(current, neighbours);

                for (int i = 0; i < neighbours.Count; i++)
                {
                    TNode next = neighbours[i];
                    if (closed.Contains(next)) continue;

                    float tentativeG = currentG + stepCost(current, next);
                    if (gScore.TryGetValue(next, out float knownG) && tentativeG >= knownG) continue;

                    cameFrom[next] = current;
                    gScore[next] = tentativeG;
                    openSet.Add(tentativeG + heuristic(next, goal), next);
                }
            }

            return null;
        }

        private static Path<TNode> Reconstruct<TNode>(
            TNode goal,
            TNode start,
            Dictionary<TNode, TNode> cameFrom,
            Func<TNode, TNode, float> stepCost,
            IEqualityComparer<TNode> comparer)
        {
            // Walk predecessors goal → start, then fold forward so accumulated cost matches travel order.
            var reversed = new List<TNode>();
            TNode node = goal;
            reversed.Add(node);
            while (!comparer.Equals(node, start))
            {
                node = cameFrom[node];
                reversed.Add(node);
            }

            Path<TNode> path = Path<TNode>.Start(start);
            for (int i = reversed.Count - 2; i >= 0; i--)
            {
                TNode from = reversed[i + 1];
                TNode to = reversed[i];
                path = path.Append(to, stepCost(from, to));
            }
            return path;
        }
    }
}
