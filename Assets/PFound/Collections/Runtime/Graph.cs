using System;
using System.Collections.Generic;

namespace PFound.Collections
{
    /// <summary>
    /// Abstraction a search consults to expand a node: given a node, append its adjacent
    /// (reachable) nodes to <paramref name="results"/>. Implementations decide connectivity,
    /// bounds and passability. Filling a caller-owned list keeps expansion allocation-free.
    /// </summary>
    public interface INeighbourProvider<TNode>
    {
        void GetNeighbours(TNode node, List<TNode> results);
    }

    /// <summary>Integer cell coordinate on a 2D board.</summary>
    public readonly struct GridCoord : IEquatable<GridCoord>
    {
        public readonly int X;
        public readonly int Y;

        public GridCoord(int x, int y) { X = x; Y = y; }

        public bool Equals(GridCoord other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridCoord other && Equals(other);
        public override int GetHashCode() => unchecked((X * 397) ^ Y);
        public override string ToString() => $"({X}, {Y})";
    }

    /// <summary>
    /// Rectangular tile board [0,width) × [0,height) that exposes cell adjacency as an
    /// <see cref="INeighbourProvider{GridCoord}"/>. Supports 4- or 8-connectivity; an optional
    /// passability predicate blocks impassable cells (and, in diagonal mode, prevents cutting the
    /// corner between two blocked orthogonal cells).
    /// </summary>
    public sealed class GridBoard : INeighbourProvider<GridCoord>
    {
        // Orthogonal steps (N, E, S, W); diagonals appended when 8-connectivity is enabled.
        private static readonly (int dx, int dy)[] Orthogonal =
            { (0, 1), (1, 0), (0, -1), (-1, 0) };
        private static readonly (int dx, int dy)[] Diagonal =
            { (1, 1), (1, -1), (-1, -1), (-1, 1) };

        private readonly Predicate<GridCoord> _isPassable;

        public int Width { get; }
        public int Height { get; }
        public bool AllowDiagonal { get; }

        /// <param name="isPassable">Returns whether an in-bounds cell may be entered. When null, every in-bounds cell is passable.</param>
        public GridBoard(int width, int height, bool allowDiagonal = false, Predicate<GridCoord> isPassable = null)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            Width = width;
            Height = height;
            AllowDiagonal = allowDiagonal;
            _isPassable = isPassable;
        }

        public bool InBounds(GridCoord cell) =>
            (uint)cell.X < (uint)Width && (uint)cell.Y < (uint)Height;

        public bool IsPassable(GridCoord cell) =>
            InBounds(cell) && (_isPassable == null || _isPassable(cell));

        public void GetNeighbours(GridCoord node, List<GridCoord> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));

            foreach (var (dx, dy) in Orthogonal)
            {
                var next = new GridCoord(node.X + dx, node.Y + dy);
                if (IsPassable(next)) results.Add(next);
            }

            if (!AllowDiagonal) return;

            foreach (var (dx, dy) in Diagonal)
            {
                var next = new GridCoord(node.X + dx, node.Y + dy);
                if (!IsPassable(next)) continue;
                // Do not slip diagonally through a corner formed by two blocked orthogonal cells.
                if (!IsPassable(new GridCoord(node.X + dx, node.Y)) &&
                    !IsPassable(new GridCoord(node.X, node.Y + dy))) continue;
                results.Add(next);
            }
        }
    }

    /// <summary>Distance estimators for grid heuristics and step costs.</summary>
    public static class GridHeuristics
    {
        /// <summary>|dx| + |dy| — admissible for 4-connectivity.</summary>
        public static float Manhattan(GridCoord a, GridCoord b) =>
            Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

        /// <summary>max(|dx|, |dy|) — admissible for 8-connectivity with uniform diagonal cost.</summary>
        public static float Chebyshev(GridCoord a, GridCoord b) =>
            Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

        /// <summary>Straight-line distance.</summary>
        public static float Euclidean(GridCoord a, GridCoord b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
