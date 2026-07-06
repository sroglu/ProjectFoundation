using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace PFound.Render.Core.RenderTextures
{
    /// <summary>
    /// Fixed-capacity, oldest-overwrite ring buffer backed by an <see cref="UnsafeList{T}"/>
    /// with <see cref="Allocator.Persistent"/>. Single-writer / multi-reader.
    /// Zero per-write managed allocations once the underlying list is preallocated.
    /// </summary>
    /// <remarks>
    /// Used by <see cref="RenderTexturePool"/> as the leak record buffer. The
    /// pool's leak-write path must not allocate; this struct's <see cref="Write"/>
    /// is intentionally allocation-free.
    /// </remarks>
    internal struct UnsafeRingBuffer<T> : IDisposable where T : unmanaged
    {
        private UnsafeList<T> _data;
        private int _head;       // next write index
        private int _count;      // current items in buffer (0..Capacity)
        private long _dropped;   // monotonic overflow counter

        public int Capacity => _data.IsCreated ? _data.Length : 0;
        public int Count => _count;
        public long DroppedCount => _dropped;

        public UnsafeRingBuffer(int capacity, Allocator allocator)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _data = new UnsafeList<T>(capacity, allocator);
            _data.Resize(capacity, NativeArrayOptions.ClearMemory);
            _head = 0;
            _count = 0;
            _dropped = 0;
        }

        public void Write(in T item)
        {
            _data[_head] = item;
            _head = (_head + 1) % _data.Length;
            if (_count < _data.Length) _count++;
            else _dropped++; // overwrite oldest
        }

        public bool TryRead(out T item)
        {
            if (_count == 0) { item = default; return false; }
            int tail = (_head - _count + _data.Length) % _data.Length;
            item = _data[tail];
            _count--;
            return true;
        }

        /// <summary>Non-destructive snapshot. Copies up to <c>dest.Length</c> entries (oldest first).</summary>
        public int Snapshot(Span<T> dest)
        {
            int copy = _count < dest.Length ? _count : dest.Length;
            int tail = (_head - _count + _data.Length) % _data.Length;
            for (int i = 0; i < copy; i++)
            {
                dest[i] = _data[(tail + i) % _data.Length];
            }
            return copy;
        }

        public void Clear()
        {
            _head = 0;
            _count = 0;
            // _dropped intentionally kept monotonic.
        }

        public void Dispose()
        {
            if (_data.IsCreated) _data.Dispose();
            _head = 0;
            _count = 0;
        }
    }
}
