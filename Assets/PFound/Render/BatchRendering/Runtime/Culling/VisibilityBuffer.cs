using System;
using Unity.Collections;

namespace PFound.Render.BatchRendering
{
    /// <summary>
    /// Reusable culled-output buffer owned by the service. One <see cref="VisibilityBuffer"/> per
    /// service instance is shared across all batches' ticks — grown lazily to the largest observed
    /// instance count and never shrunk (avoids reallocation churn when a batch's count oscillates).
    /// </summary>
    /// <remarks>
    /// Backed by a <see cref="NativeList{T}"/> so Burst cull jobs can append from worker threads via
    /// <see cref="NativeList{T}.ParallelWriter"/> + <c>AddNoResize</c>. The pre-tick <see cref="Reset"/>
    /// clears the list; the post-tick <see cref="VisibleCount"/> reads <see cref="NativeList{T}.Length"/>.
    /// </remarks>
    internal sealed class VisibilityBuffer : IDisposable
    {
        private NativeList<int> _visibleIndices;
        private bool _disposed;

        internal VisibilityBuffer()
        {
            // Allocate with a small starting capacity; first EnsureCapacity for a real batch resizes
            // to the right value.
            _visibleIndices = new NativeList<int>(0, Allocator.Persistent);
        }

        /// <summary>Current backing capacity (NativeList's pre-allocated slot count).</summary>
        internal int Capacity => _visibleIndices.IsCreated ? _visibleIndices.Capacity : 0;

        /// <summary>The indices list the cull job appends into. Use <c>AsArray()</c> for backend
        /// consumption (returns a view of the populated prefix).</summary>
        internal NativeList<int> VisibleIndices => _visibleIndices;

        /// <summary>
        /// Ensures <see cref="VisibleIndices"/> has at least <paramref name="required"/> pre-allocated
        /// slots so cull jobs can use <c>AddNoResize</c>. Grows in 2× steps; never shrinks.
        /// </summary>
        internal void EnsureCapacity(int required)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(VisibilityBuffer));
            if (required <= _visibleIndices.Capacity) return;

            int newCap = _visibleIndices.Capacity == 0 ? Math.Max(required, 64) : _visibleIndices.Capacity * 2;
            if (newCap < required) newCap = required;

            _visibleIndices.Capacity = newCap;
        }

        /// <summary>Clears the list; called at the start of every per-batch per-camera cull.</summary>
        internal void Reset()
        {
            if (_disposed) return;
            _visibleIndices.Clear();
        }

        /// <summary>
        /// Main-thread setter for the visible count. Sets the underlying NativeList length to
        /// <paramref name="count"/> (the consumer fills entries [0..count) directly via
        /// <see cref="VisibleIndices"/> or <c>AsArray()</c>). Used by the all-visible pass-through
        /// path (<see cref="CullingPolicy.None"/>) and tests.
        /// </summary>
        internal void SetVisibleCount(int count)
        {
            if (_disposed) return;
            _visibleIndices.Length = count;
        }

        /// <summary>Post-tick visible count.</summary>
        internal int VisibleCount => _disposed ? 0 : _visibleIndices.Length;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_visibleIndices.IsCreated) _visibleIndices.Dispose();
        }
    }
}
