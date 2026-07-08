using System;
using System.Collections;
using System.Collections.Generic;

namespace PFound.Collections
{
    /// <summary>
    /// An immutable step chain from a start node to the current node. Each instance stores its
    /// node, its predecessor and the total <see cref="Cost"/> accumulated from the start.
    /// Extending a path allocates a new tail node and shares the prefix, so the original path is
    /// never mutated. Enumeration yields nodes in travel order (start → end).
    /// </summary>
    public sealed class Path<TNode> : IEnumerable<TNode>
    {
        /// <summary>The node this step lands on.</summary>
        public TNode Node { get; }

        /// <summary>The step before this one, or <c>null</c> when this is the start (chain terminator).</summary>
        public Path<TNode> Previous { get; }

        /// <summary>Total cost accumulated from the start node up to and including this step.</summary>
        public float Cost { get; }

        /// <summary>Number of nodes in the chain (1 for a start-only path).</summary>
        public int Length { get; }

        private Path(TNode node, Path<TNode> previous, float cost, int length)
        {
            Node = node;
            Previous = previous;
            Cost = cost;
            Length = length;
        }

        /// <summary>Creates a single-node path rooted at <paramref name="node"/> with zero cost.</summary>
        public static Path<TNode> Start(TNode node) => new Path<TNode>(node, null, 0f, 1);

        /// <summary>Returns a new path that appends <paramref name="node"/> at an added <paramref name="stepCost"/>.</summary>
        public Path<TNode> Append(TNode node, float stepCost) =>
            new Path<TNode>(node, this, Cost + stepCost, Length + 1);

        public IEnumerator<TNode> GetEnumerator()
        {
            // The chain is linked tail → head; buffer it and replay in start → end order.
            var buffer = new TNode[Length];
            int i = Length;
            for (Path<TNode> step = this; step != null; step = step.Previous)
                buffer[--i] = step.Node;
            for (int k = 0; k < buffer.Length; k++)
                yield return buffer[k];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
