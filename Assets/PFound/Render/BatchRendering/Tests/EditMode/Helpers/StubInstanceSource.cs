using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using PFound.Render.BatchRendering;

namespace PFound.Render.Tests.BatchRendering
{
    /// <summary>
    /// Test-only minimal <see cref="IBatchInstanceSource"/> implementation. Returns a fixed
    /// <see cref="Count"/> and synthesizes per-instance matrices on demand into a private
    /// <see cref="NativeArray{T}"/> the caller is responsible for disposing via
    /// <see cref="DisposeBacking"/>. Used by foundational tests that need a source instance to
    /// register a batch but don't care about the actual instance data.
    /// </summary>
    internal sealed class StubInstanceSource : IBatchInstanceSource
    {
        private NativeArray<float4x4> _backing;
        public int Count { get; set; }

        public StubInstanceSource(int count)
        {
            Count = count;
            _backing = new NativeArray<float4x4>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            for (int i = 0; i < count; i++) _backing[i] = float4x4.identity;
        }

        public bool TryGetNativeArrayView(out NativeArray<float4x4> view)
        {
            if (Count <= 0)
            {
                view = default;
                return _backing.IsCreated; // still a valid CPU-side source, just empty
            }
            view = _backing.GetSubArray(0, Mathf.Min(Count, _backing.Length));
            return true;
        }

        public bool TryGetComputeBuffer(out ComputeBuffer buffer, out int stride)
        {
            buffer = null;
            stride = 0;
            return false;
        }

        public void OnTickBegin(JobHandle dependency, out JobHandle producedHandle)
        {
            producedHandle = dependency;
        }

        public void DisposeBacking()
        {
            if (_backing.IsCreated) _backing.Dispose();
        }
    }
}
