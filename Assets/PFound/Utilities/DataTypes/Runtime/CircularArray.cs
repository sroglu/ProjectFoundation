using System;
using System.Collections;
using System.Collections.Generic;

namespace PFound.Utilities.DataType
{
    /// <summary>
    /// Fixed-capacity ring buffer. Appending past the capacity overwrites the oldest element.
    /// Enumeration and indexing run from oldest to newest.
    /// </summary>
    public sealed class CircularArray<T> : ICollection<T>, IReadOnlyCollection<T>
    {
        private readonly T[] _items;
        private int _head;  // index where the next element will be written
        private int _count;

        public CircularArray(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
            _items = new T[capacity];
        }

        /// <summary>Maximum number of elements the buffer can hold.</summary>
        public int Capacity => _items.Length;

        /// <summary>Number of elements currently stored.</summary>
        public int Count => _count;

        public bool IsFull => _count == _items.Length;

        public bool IsEmpty => _count == 0;

        bool ICollection<T>.IsReadOnly => false;

        /// <summary>Oldest-to-newest indexer. Index 0 is the oldest retained element.</summary>
        public T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
                return _items[TailToPhysical(index)];
            }
            set
            {
                if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
                _items[TailToPhysical(index)] = value;
            }
        }

        /// <summary>
        /// Appends an element. When the buffer is full the oldest element is overwritten and dropped.
        /// </summary>
        public void Append(T item)
        {
            _items[_head] = item;
            _head = Increment(_head);
            if (_count < _items.Length) _count++;
        }

        void ICollection<T>.Add(T item) => Append(item);

        /// <summary>The newest element. Throws when empty.</summary>
        public T Newest
        {
            get
            {
                if (_count == 0) throw new InvalidOperationException("Buffer is empty.");
                return _items[Decrement(_head)];
            }
        }

        /// <summary>The oldest retained element. Throws when empty.</summary>
        public T Oldest
        {
            get
            {
                if (_count == 0) throw new InvalidOperationException("Buffer is empty.");
                return _items[TailToPhysical(0)];
            }
        }

        public void Clear()
        {
            Array.Clear(_items, 0, _items.Length);
            _head = 0;
            _count = 0;
        }

        public bool Contains(T item)
        {
            var comparer = EqualityComparer<T>.Default;
            for (var i = 0; i < _count; i++)
            {
                if (comparer.Equals(_items[TailToPhysical(i)], item)) return true;
            }
            return false;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));
            if (arrayIndex < 0 || arrayIndex + _count > array.Length)
                throw new ArgumentOutOfRangeException(nameof(arrayIndex));

            for (var i = 0; i < _count; i++)
            {
                array[arrayIndex + i] = _items[TailToPhysical(i)];
            }
        }

        /// <summary>Removal from arbitrary positions is not supported by a ring buffer.</summary>
        bool ICollection<T>.Remove(T item) =>
            throw new NotSupportedException("CircularArray does not support arbitrary removal.");

        public IEnumerator<T> GetEnumerator()
        {
            for (var i = 0; i < _count; i++)
            {
                yield return _items[TailToPhysical(i)];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private int TailToPhysical(int logicalIndex)
        {
            // Oldest element sits _count slots behind the head.
            var tail = _head - _count;
            if (tail < 0) tail += _items.Length;
            var physical = tail + logicalIndex;
            if (physical >= _items.Length) physical -= _items.Length;
            return physical;
        }

        private int Increment(int index) => index + 1 == _items.Length ? 0 : index + 1;

        private int Decrement(int index) => index == 0 ? _items.Length - 1 : index - 1;
    }
}
