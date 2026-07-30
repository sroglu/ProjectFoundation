using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace PFound.Render.BatchRendering
{
    /// <summary>
    /// CPU-side <see cref="IBatchInstanceSource"/> wrapping a consumer-owned
    /// <see cref="NativeArray{T}"/> of <see cref="float4x4"/> per-instance transforms.
    /// </summary>
    /// <remarks>
    /// The consumer owns the underlying <see cref="NativeArray{T}"/> and is responsible for
    /// disposing it after all batches referencing this source have been disposed. This source does
    /// NOT dispose the array.
    /// <para>
    /// <see cref="SetCount"/> allows partial-fill scenarios: consumer pre-allocates a large array
    /// (e.g., 10,000) but only the first <see cref="Count"/> entries are live on a given frame.
    /// </para>
    /// </remarks>
    public sealed class NativeArrayInstanceSource : IBatchInstanceSource
    {
        private readonly NativeArray<float4x4> _data;
        private int _count;

        /// <summary>
        /// Wraps <paramref name="data"/>. If <paramref name="count"/> is supplied, only the prefix
        /// <c>[0, count)</c> is exposed; otherwise the full array length is used.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="data"/> is not created.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="count"/> is negative or
        /// greater than <c>data.Length</c>.</exception>
        public NativeArrayInstanceSource(NativeArray<float4x4> data, int? count = null)
        {
            if (!data.IsCreated)
                throw new ArgumentNullException(nameof(data), "NativeArray<float4x4> must be created (allocated) before wrapping.");

            int c = count ?? data.Length;
            if (c < 0 || c > data.Length)
                throw new ArgumentOutOfRangeException(nameof(count), c, $"count must be in [0, {data.Length}].");

            _data = data;
            _count = c;
        }

        /// <inheritdoc/>
        public int Count => _count;

        /// <summary>
        /// Overrides the exposed count. Must be in <c>[0, data.Length]</c>. Callable between ticks;
        /// not safe to call during a tick (cull jobs read <see cref="Count"/> at OnTickBegin).
        /// </summary>
        public void SetCount(int count)
        {
            if (count < 0 || count > _data.Length)
                throw new ArgumentOutOfRangeException(nameof(count), count, $"count must be in [0, {_data.Length}].");
            _count = count;
        }

        /// <inheritdoc/>
        public bool TryGetNativeArrayView(out NativeArray<float4x4> view)
        {
            if (_count == 0)
            {
                // Still a valid CPU-side source; return an empty slice (caller short-circuits on
                // Count == 0 anyway).
                view = default;
                return true;
            }
            view = _data.GetSubArray(0, Mathf.Min(_count, _data.Length));
            return true;
        }

        /// <inheritdoc/>
        public bool TryGetComputeBuffer(out ComputeBuffer buffer, out int stride)
        {
            buffer = null;
            stride = 0;
            return false;
        }

        /// <inheritdoc/>
        public void OnTickBegin(JobHandle dependency, out JobHandle producedHandle)
        {
            // Pure data source — no per-tick prep work. Passes the dependency through unchanged.
            producedHandle = dependency;
        }
    }
}
