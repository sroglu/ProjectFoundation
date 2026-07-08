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

        /// <summary>Appends every element of <paramref name="items"/> in order.</summary>
        public void AddRange(ReadOnlySpan<T> items)
        {
            if (items.Length == 0) return;
            EnsureCapacity(_count + items.Length);
            items.CopyTo(new Span<T>(_items, _count, items.Length));
            _count += items.Length;
        }

        /// <summary>Inserts <paramref name="item"/> at <paramref name="index"/>, shifting later elements right (order preserved).</summary>
        public void Insert(int index, in T item)
        {
            if ((uint)index > (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
            EnsureCapacity(_count + 1);
            if (index < _count) Array.Copy(_items, index, _items, index + 1, _count - index);
            _items[index] = item;
            _count++;
        }

        /// <summary>Claims the next slot and returns it by ref WITHOUT clearing — caller initializes it.</summary>
        public ref T AddWithoutInitializing()
        {
            EnsureCapacity(_count + 1);
            return ref _items[_count++];
        }

        /// <summary>Removes index <paramref name="index"/> in O(n), shifting later elements left (order preserved).</summary>
        public void RemoveAt(int index)
        {
            if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
            _count--;
            if (index < _count) Array.Copy(_items, index + 1, _items, index, _count - index);
            _items[_count] = default; // release the vacated slot (managed refs)
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

        /// <summary>A writable span over the sub-range [start, start+length). Invalidated by add/remove.</summary>
        public Span<T> AsSpan(int start, int length)
        {
            ValidateRange(start, length);
            return new Span<T>(_items, start, length);
        }

        /// <summary>A read-only span over the live elements [0, Count). Invalidated by add/remove.</summary>
        public ReadOnlySpan<T> AsReadOnlySpan() => new ReadOnlySpan<T>(_items, 0, _count);

        /// <summary>A read-only span over the sub-range [start, start+length). Invalidated by add/remove.</summary>
        public ReadOnlySpan<T> AsReadOnlySpan(int start, int length)
        {
            ValidateRange(start, length);
            return new ReadOnlySpan<T>(_items, start, length);
        }

        /// <summary>Copies the live elements [0, Count) into a freshly allocated array.</summary>
        public T[] ToArray()
        {
            var result = new T[_count];
            Array.Copy(_items, result, _count);
            return result;
        }

        /// <summary>The backing array itself (length == <see cref="Capacity"/>, not <see cref="Count"/>). For zero-copy interop; mutate with care.</summary>
        public T[] GetInternalArray() => _items;

        /// <summary>Allocation-free enumerator over the live elements [0, Count) for <c>foreach</c>.</summary>
        public Enumerator GetEnumerator() => new Enumerator(this);

        public void Clear()
        {
            Array.Clear(_items, 0, _count);
            _count = 0;
        }

        private void ValidateRange(int start, int length)
        {
            if (start < 0) throw new ArgumentOutOfRangeException(nameof(start));
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            if (start + length > _count) throw new ArgumentException("Range [start, start+length) exceeds Count.");
        }

        private void EnsureCapacity(int min)
        {
            if (min <= _items.Length) return;
            int capacity = _items.Length * 2;
            if (capacity < min) capacity = min;
            Array.Resize(ref _items, capacity);
        }

        /// <summary>Forward struct enumerator; <see cref="Current"/> is a <c>ref</c> for in-place read/mutate.</summary>
        public struct Enumerator
        {
            private readonly StructList<T> _list;
            private int _index;

            internal Enumerator(StructList<T> list) { _list = list; _index = -1; }

            public bool MoveNext() => ++_index < _list._count;
            public ref T Current => ref _list._items[_index];
        }
    }
}
