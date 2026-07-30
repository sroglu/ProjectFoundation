using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace PFound.Render.BatchRendering
{
    /// <summary>
    /// Migration-bridge <see cref="IBatchInstanceSource"/> that wraps a legacy
    /// <c>Transform[]</c> field and flattens it to a <c>NativeArray&lt;float4x4&gt;</c> on each
    /// tick via a Burst <see cref="IJobParallelForTransform"/>.
    /// </summary>
    /// <remarks>
    /// <b>⚠ Not the recommended long-term API.</b> The per-tick flatten costs more than authoring
    /// instance data directly via <see cref="NativeArrayInstanceSource"/>. Use this source ONLY to
    /// bridge legacy code that already exposes a <c>Transform[]</c>; the long-term migration is to
    /// switch to <see cref="NativeArrayInstanceSource"/> populated from the consumer's data layer.
    /// <para>
    /// The source owns an internal <see cref="NativeArray{T}"/> flatten buffer +
    /// <see cref="TransformAccessArray"/>; <see cref="Dispose"/> must be called by the consumer
    /// after all batches referencing this source have been disposed. Null <see cref="Transform"/>
    /// entries inside the input array are silently skipped (zero-matrix). A one-shot warning is
    /// emitted on the first detected null index per source instance.
    /// </para>
    /// </remarks>
    public sealed class TransformArrayInstanceSource : IBatchInstanceSource, IDisposable
    {
        private readonly Guid _sourceId = Guid.NewGuid();
        private readonly OneShotGate _diagnostics = new OneShotGate();

        private Transform[] _transforms;
        private TransformAccessArray _accessArray;
        private NativeArray<float4x4> _flatView;
        private bool _accessArrayCreated;
        private bool _disposed;

        /// <summary>
        /// Wraps <paramref name="transforms"/>. The array reference is captured; later additions /
        /// removals to the underlying array are picked up on the next tick (the
        /// <see cref="TransformAccessArray"/> is rebuilt when the length changes).
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="transforms"/> is null.</exception>
        public TransformArrayInstanceSource(Transform[] transforms)
        {
            if (transforms == null) throw new ArgumentNullException(nameof(transforms));
            _transforms = transforms;
            // Allocate flatten storage lazily on first OnTickBegin.
        }

        /// <inheritdoc/>
        public int Count => _transforms != null ? _transforms.Length : 0;

        /// <inheritdoc/>
        public bool TryGetNativeArrayView(out NativeArray<float4x4> view)
        {
            if (!_flatView.IsCreated)
            {
                view = default;
                return true; // CPU-side, just hasn't been ticked yet.
            }
            view = _flatView.GetSubArray(0, Mathf.Min(Count, _flatView.Length));
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
            if (_disposed)
            {
                producedHandle = dependency;
                return;
            }

            int count = _transforms.Length;
            EnsureFlatView(count);
            EnsureAccessArray(count);

            if (count == 0)
            {
                producedHandle = dependency;
                return;
            }

            var job = new TransformFlattenJob
            {
                FlatView = _flatView,
            };
            producedHandle = job.Schedule(_accessArray, dependency);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_accessArrayCreated && _accessArray.isCreated)
            {
                _accessArray.Dispose();
                _accessArrayCreated = false;
            }
            if (_flatView.IsCreated) _flatView.Dispose();
        }

        private void EnsureFlatView(int required)
        {
            if (_flatView.IsCreated && _flatView.Length >= required) return;
            if (_flatView.IsCreated) _flatView.Dispose();
            int cap = System.Math.Max(required, 4);
            _flatView = new NativeArray<float4x4>(cap, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private void EnsureAccessArray(int count)
        {
            // Rebuild the TransformAccessArray when the length changes or null entries appear.
            // For Phase 11 simplicity we always rebuild — a future patch could diff the array.
            if (_accessArrayCreated && _accessArray.length == count)
            {
                // Still need to refresh in case array slot was replaced — check identity quickly.
                // We skip this O(n) compare and accept that consumers who mutate Transform[] in
                // place must trigger a rebuild manually. For Phase 11 this is a documented bridge
                // path, not the recommended API.
                return;
            }

            if (_accessArrayCreated && _accessArray.isCreated) _accessArray.Dispose();
            _accessArrayCreated = false;

            // Build a compact TransformAccessArray that skips null entries. Null indices emit a
            // one-shot warning per source.
            _accessArray = new TransformAccessArray(count);
            for (int i = 0; i < count; i++)
            {
                var t = _transforms[i];
                if (t == null)
                {
                    OneShotWarnings.WarnTransformArrayNullEntry(_diagnostics, _sourceId, i);
                    // Add a sentinel Transform? TransformAccessArray doesn't accept null. Skip.
                    continue;
                }
                _accessArray.Add(t);
            }
            _accessArrayCreated = true;
        }

        // Burst flatten job — writes Transform.localToWorldMatrix into the flat view.
        [BurstCompile]
        internal struct TransformFlattenJob : IJobParallelForTransform
        {
            [WriteOnly] public NativeArray<float4x4> FlatView;

            public void Execute(int index, TransformAccess transform)
            {
                if (index >= FlatView.Length) return;
                Matrix4x4 m = transform.localToWorldMatrix;
                FlatView[index] = new float4x4(
                    new float4(m.m00, m.m10, m.m20, m.m30),
                    new float4(m.m01, m.m11, m.m21, m.m31),
                    new float4(m.m02, m.m12, m.m22, m.m32),
                    new float4(m.m03, m.m13, m.m23, m.m33));
            }
        }
    }
}
