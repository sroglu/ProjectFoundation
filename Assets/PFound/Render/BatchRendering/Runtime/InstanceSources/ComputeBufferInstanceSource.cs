using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace PFound.Render.BatchRendering
{
    /// <summary>
    /// GPU-side <see cref="IBatchInstanceSource"/> wrapping a consumer-owned
    /// <see cref="ComputeBuffer"/> of per-instance data. Used with the
    /// <see cref="BackendKind.Indirect"/> and <see cref="BackendKind.Procedural"/> backends.
    /// </summary>
    /// <remarks>
    /// The consumer owns the <see cref="ComputeBuffer"/> and is responsible for disposing it after
    /// all batches referencing this source have been disposed. This source does NOT release the
    /// buffer.
    /// <para>
    /// The stride is captured at construction; for the standard <see cref="MeshInstanceData"/>
    /// layout, <see cref="MeshInstanceDataStride"/> (= 80) is the correct value. Custom layouts are
    /// allowed when the matching backend is <see cref="BackendKind.Procedural"/> (consumer-owned
    /// dispatch path).
    /// </para>
    /// </remarks>
    public sealed class ComputeBufferInstanceSource : IBatchInstanceSource
    {
        /// <summary>Stride (in bytes) of the standard <see cref="MeshInstanceData"/> layout.</summary>
        public const int MeshInstanceDataStride = 80;

        private readonly ComputeBuffer _buffer;
        private readonly int _stride;
        private int _count;

        /// <summary>
        /// Wraps <paramref name="buffer"/>. If <paramref name="count"/> is supplied, only the prefix
        /// <c>[0, count)</c> is exposed; otherwise <c>buffer.count</c> is used.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="buffer"/> is null or
        /// not valid (<c>IsValid() == false</c>).</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="stride"/> &lt;= 0,
        /// or <paramref name="count"/> is negative or greater than <c>buffer.count</c>.</exception>
        public ComputeBufferInstanceSource(ComputeBuffer buffer, int stride, int? count = null)
        {
            if (buffer == null || !buffer.IsValid())
                throw new ArgumentNullException(nameof(buffer), "ComputeBuffer must be non-null and valid (IsValid()).");
            if (stride <= 0)
                throw new ArgumentOutOfRangeException(nameof(stride), stride, "stride must be > 0.");

            int c = count ?? buffer.count;
            if (c < 0 || c > buffer.count)
                throw new ArgumentOutOfRangeException(nameof(count), c, $"count must be in [0, {buffer.count}].");

            _buffer = buffer;
            _stride = stride;
            _count = c;
        }

        /// <inheritdoc/>
        public int Count => _count;

        /// <summary>The wrapped <see cref="ComputeBuffer"/>. Consumer-owned.</summary>
        public ComputeBuffer Buffer => _buffer;

        /// <summary>Per-element stride captured at construction.</summary>
        public int Stride => _stride;

        /// <summary>
        /// Overrides the exposed count. Must be in <c>[0, buffer.count]</c>.
        /// </summary>
        public void SetCount(int count)
        {
            if (count < 0 || count > _buffer.count)
                throw new ArgumentOutOfRangeException(nameof(count), count, $"count must be in [0, {_buffer.count}].");
            _count = count;
        }

        /// <inheritdoc/>
        public bool TryGetNativeArrayView(out NativeArray<float4x4> view)
        {
            view = default;
            return false;
        }

        /// <inheritdoc/>
        public bool TryGetComputeBuffer(out ComputeBuffer buffer, out int stride)
        {
            buffer = _buffer;
            stride = _stride;
            return true;
        }

        /// <inheritdoc/>
        public void OnTickBegin(JobHandle dependency, out JobHandle producedHandle)
        {
            // Pure GPU-side source — no per-tick CPU prep work.
            producedHandle = dependency;
        }
    }
}
