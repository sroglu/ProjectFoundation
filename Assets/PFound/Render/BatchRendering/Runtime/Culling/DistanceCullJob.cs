using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace PFound.Render.BatchRendering
{
    /// <summary>
    /// Burst-compiled distance cull job — chained after <see cref="FrustumCullJob"/>. Reads the
    /// frustum-visible index list and filters out instances whose distance from the active camera
    /// exceeds <see cref="MaxDistanceSq"/>.
    /// </summary>
    /// <remarks>
    /// Designed to consume the frustum job's output and produce a tighter index list. Both jobs
    /// write to the same <see cref="VisibilityBuffer"/> NativeList — distance reads the frustum
    /// result by indexing into the matrices array directly (each frustum-visible index points to a
    /// matrix; we read the matrix translation column for the world-space center).
    /// <para>
    /// Single-element <c>IJobParallelFor</c> over <c>FrustumVisibleCount</c> — re-uses the same
    /// batch size (64) as the frustum job. Output appended into a fresh list (the service swaps
    /// the visibility buffer between the two stages, or uses a two-step compaction). For Phase 11
    /// simplicity, the service runs the distance pass single-threaded after the frustum job since
    /// the additional Burst overhead at typical counts (≤50k) is negligible — chained-Burst
    /// optimization deferred.
    /// </para>
    /// </remarks>
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
    internal struct DistanceCullJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float4x4> Matrices;
        [ReadOnly] public NativeArray<int> FrustumVisibleIndices;
        [ReadOnly] public int FrustumVisibleCount;
        [ReadOnly] public float3 CameraPosition;
        [ReadOnly] public float MaxDistanceSq;

        public NativeList<int>.ParallelWriter FinalVisibleIndices;

        public void Execute(int i)
        {
            if (i >= FrustumVisibleCount) return;
            int srcIdx = FrustumVisibleIndices[i];
            float4x4 ltw = Matrices[srcIdx];
            // Translation column = world-space position of the instance origin.
            float3 pos = ltw.c3.xyz;
            float3 diff = pos - CameraPosition;
            float distSq = math.dot(diff, diff);
            if (distSq <= MaxDistanceSq)
            {
                FinalVisibleIndices.AddNoResize(srcIdx);
            }
        }
    }
}
