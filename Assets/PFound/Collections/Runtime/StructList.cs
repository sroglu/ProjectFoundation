using System;

namespace PFound.Collections
{
    /// <summary>
    /// A growable list optimized for value-type elements: a <see cref="this[int]"/> that returns a
    /// <c>ref</c> for in-place mutation, <see cref="AsSpan"/> for zero-copy bulk access,
    /// <see cref="AddWithoutInitializing"/> to claim a slot without clearing it, and O(1)
    /// <see cref="RemoveAtSwapBack"/>. Backed by a single array; main-thread only.
    /// </summary>
    public sealed class StructList<T>
    {
        private T[] _items;
        private int _count;

        public StructList(int capacity = 4) { _items = new T[capacity < 1 ? 1 : capacity]; }

        public int Count => _count;
        public int Capacity => _items.Length;

        /// <summary>In-place element access by reference (read or mutate without copying).</summary>
        public ref T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
                return ref _items[index];
            }
        }

        public void Add(in T item)
        {
            EnsureCapacity(_count + 1);
            _items[_count++] = item;
        }

        /// <summary>Claims the next slot and returns it by ref WITHOUT clearing — caller initializes it.</summary>
        public ref T AddWithoutInitializing()
        {
            EnsureCapacity(_count + 1);
            return ref _items[_count++];
        }

        /// <summary>Removes index <paramref name="index"/> in O(1) by moving the last element into its place (order not preserved).</summary>
        public void RemoveAtSwapBack(int index)
        {
            if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
            _count--;
            _items[index] = _items[_count];
            _items[_count] = default; // release the vacated slot (managed refs)
        }

        /// <summary>A span over the live elements [0, Count). Reflects in-place edits; invalidated by add/remove.</summary>
        public Span<T> AsSpan() => new Span<T>(_items, 0, _count);

        public void Clear()
        {
            Array.Clear(_items, 0, _count);
            _count = 0;
        }

        private void EnsureCapacity(int min)
        {
            if (min <= _items.Length) return;
            int capacity = _items.Length * 2;
            if (capacity < min) capacity = min;
            Array.Resize(ref _items, capacity);
        }
    }
}
