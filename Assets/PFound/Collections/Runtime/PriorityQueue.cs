using System;
using System.Collections.Generic;

namespace PFound.Collections
{
    /// <summary>
    /// Double-ended priority queue backed by a min-max heap: O(log n) <see cref="Add"/>,
    /// <see cref="PopMin"/> and <see cref="PopMax"/>, O(1) <see cref="Min"/>/<see cref="Max"/> peeks.
    /// Ordering is by <typeparamref name="TKey"/> via the supplied (or default) comparer.
    /// Main-thread only.
    /// </summary>
    public sealed class PriorityQueue<TKey, TValue>
    {
        private struct Node { public TKey Key; public TValue Value; }

        private Node[] _nodes;
        private int _count;
        private readonly IComparer<TKey> _comparer;

        public PriorityQueue(int capacity = 8, IComparer<TKey> comparer = null)
        {
            _nodes = new Node[capacity < 1 ? 1 : capacity];
            _comparer = comparer ?? Comparer<TKey>.Default;
        }

        public int Count => _count;
        public bool IsEmpty => _count == 0;

        public void Clear()
        {
            Array.Clear(_nodes, 0, _count);
            _count = 0;
        }

        /// <summary>
        /// Removes the first entry whose value equals <paramref name="value"/> (by
        /// <see cref="EqualityComparer{TValue}.Default"/>) and re-establishes the heap. O(n).
        /// Returns <c>true</c> if an entry was found and removed.
        /// </summary>
        public bool Remove(TValue value)
        {
            var comparer = EqualityComparer<TValue>.Default;
            for (int i = 0; i < _count; i++)
            {
                if (comparer.Equals(_nodes[i].Value, value))
                {
                    _count--;
                    if (i != _count) _nodes[i] = _nodes[_count];
                    _nodes[_count] = default;
                    Heapify();
                    return true;
                }
            }
            return false;
        }

        /// <summary>Allocation-free enumerator over the contained values, in unspecified (heap) order.</summary>
        public Enumerator GetEnumerator() => new Enumerator(this);

        // Floyd build-heap: trickle every internal node down, last-parent → root. O(n), used to
        // repair the heap after an arbitrary interior removal.
        private void Heapify()
        {
            for (int i = _count / 2 - 1; i >= 0; i--) TrickleDown(i);
        }

        public void Add(TKey key, TValue value)
        {
            EnsureCapacity(_count + 1);
            _nodes[_count] = new Node { Key = key, Value = value };
            BubbleUp(_count);
            _count++;
        }

        public TValue Min { get { RequireNonEmpty(); return _nodes[0].Value; } }
        public TValue Max { get { RequireNonEmpty(); return _nodes[MaxIndex()].Value; } }

        public TValue PopMin()
        {
            RequireNonEmpty();
            var result = _nodes[0].Value;
            RemoveAt(0);
            return result;
        }

        public TValue PopMax()
        {
            RequireNonEmpty();
            int index = MaxIndex();
            var result = _nodes[index].Value;
            RemoveAt(index);
            return result;
        }

        // ---- heap internals (min-max heap; even levels = min, odd levels = max) ----

        private void RemoveAt(int index)
        {
            _count--;
            if (index == _count) { _nodes[_count] = default; return; }
            _nodes[index] = _nodes[_count];
            _nodes[_count] = default;
            TrickleDown(index);
        }

        private int MaxIndex()
        {
            if (_count == 1) return 0;
            if (_count == 2) return 1;
            return Cmp(1, 2) >= 0 ? 1 : 2;
        }

        private void BubbleUp(int i)
        {
            if (i == 0) return;
            int parent = (i - 1) / 2;
            if (IsMinLevel(i))
            {
                if (Cmp(i, parent) > 0) { Swap(i, parent); BubbleUpMax(parent); }
                else BubbleUpMin(i);
            }
            else
            {
                if (Cmp(i, parent) < 0) { Swap(i, parent); BubbleUpMin(parent); }
                else BubbleUpMax(i);
            }
        }

        private void BubbleUpMin(int i)
        {
            while (i > 2)
            {
                int gp = ((i - 1) / 2 - 1) / 2;
                if (Cmp(i, gp) < 0) { Swap(i, gp); i = gp; }
                else break;
            }
        }

        private void BubbleUpMax(int i)
        {
            while (i > 2)
            {
                int gp = ((i - 1) / 2 - 1) / 2;
                if (Cmp(i, gp) > 0) { Swap(i, gp); i = gp; }
                else break;
            }
        }

        private void TrickleDown(int i)
        {
            if (IsMinLevel(i)) TrickleDownMin(i);
            else TrickleDownMax(i);
        }

        private void TrickleDownMin(int i)
        {
            while (true)
            {
                int m = BestDescendant(i, smallest: true, out bool isGrandchild);
                if (m == -1) break;
                if (isGrandchild)
                {
                    if (Cmp(m, i) < 0)
                    {
                        Swap(m, i);
                        int parent = (m - 1) / 2;
                        if (Cmp(m, parent) > 0) Swap(m, parent);
                        i = m;
                    }
                    else break;
                }
                else
                {
                    if (Cmp(m, i) < 0) Swap(m, i);
                    break;
                }
            }
        }

        private void TrickleDownMax(int i)
        {
            while (true)
            {
                int m = BestDescendant(i, smallest: false, out bool isGrandchild);
                if (m == -1) break;
                if (isGrandchild)
                {
                    if (Cmp(m, i) > 0)
                    {
                        Swap(m, i);
                        int parent = (m - 1) / 2;
                        if (Cmp(m, parent) < 0) Swap(m, parent);
                        i = m;
                    }
                    else break;
                }
                else
                {
                    if (Cmp(m, i) > 0) Swap(m, i);
                    break;
                }
            }
        }

        // Best (smallest or largest) among i's children + grandchildren, or -1 if i is a leaf.
        private int BestDescendant(int i, bool smallest, out bool isGrandchild)
        {
            int firstChild = 2 * i + 1;
            isGrandchild = false;
            if (firstChild >= _count) return -1;

            int secondChild = 2 * i + 2;
            int best = firstChild;
            if (secondChild < _count && Better(secondChild, best, smallest)) best = secondChild;
            for (int g = 4 * i + 3; g <= 4 * i + 6 && g < _count; g++)
                if (Better(g, best, smallest)) best = g;

            isGrandchild = best > secondChild;
            return best;
        }

        private bool Better(int a, int b, bool smallest) => smallest ? Cmp(a, b) < 0 : Cmp(a, b) > 0;

        private static bool IsMinLevel(int index)
        {
            int level = 0;
            for (int x = index + 1; x > 1; x >>= 1) level++;
            return (level & 1) == 0;
        }

        private int Cmp(int a, int b) => _comparer.Compare(_nodes[a].Key, _nodes[b].Key);

        private void Swap(int a, int b) { var t = _nodes[a]; _nodes[a] = _nodes[b]; _nodes[b] = t; }

        private void RequireNonEmpty()
        {
            if (_count == 0) throw new InvalidOperationException("PriorityQueue is empty.");
        }

        private void EnsureCapacity(int min)
        {
            if (min <= _nodes.Length) return;
            int capacity = _nodes.Length * 2;
            if (capacity < min) capacity = min;
            Array.Resize(ref _nodes, capacity);
        }

        /// <summary>Forward struct enumerator over the contained values (heap order, not priority order).</summary>
        public struct Enumerator
        {
            private readonly PriorityQueue<TKey, TValue> _queue;
            private int _index;

            internal Enumerator(PriorityQueue<TKey, TValue> queue) { _queue = queue; _index = -1; }

            public bool MoveNext() => ++_index < _queue._count;
            public TValue Current => _queue._nodes[_index].Value;
        }
    }
}
