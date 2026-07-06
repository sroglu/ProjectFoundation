using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace PFound.Render.Tests.BatchRendering
{
    /// <summary>
    /// Covers FR-033 + SC-005 — distance cull threshold semantics. Schedules the real Burst
    /// <see cref="PFound.Render.BatchRendering.DistanceCullJob"/> with a synthesized matrix
    /// field and verifies the visible-count matches the threshold.
    /// </summary>
    public sealed class DistanceCullThresholdTests
    {
        private static int RunDistanceCull(
            NativeArray<float4x4> matrices,
            NativeArray<int> frustumIndices,
            int frustumCount,
            float3 cameraPos,
            float maxDistanceSq)
        {
            var final = new NativeList<int>(frustumCount, Allocator.TempJob);
            try
            {
                final.Capacity = frustumCount;
                var job = new PFound.Render.BatchRendering.DistanceCullJob
                {
                    Matrices = matrices,
                    FrustumVisibleIndices = frustumIndices,
                    FrustumVisibleCount = frustumCount,
                    CameraPosition = cameraPos,
                    MaxDistanceSq = maxDistanceSq,
                    FinalVisibleIndices = final.AsParallelWriter(),
                };
                job.Schedule(frustumCount, 64).Complete();
                return final.Length;
            }
            finally { final.Dispose(); }
        }

        [Test]
        public void AllInsideThreshold_AllPass()
        {
            // 10 instances at distance 5 from camera; maxDist = 10 → all pass.
            var matrices = new NativeArray<float4x4>(10, Allocator.TempJob);
            var frustum = new NativeArray<int>(10, Allocator.TempJob);
            try
            {
                for (int i = 0; i < 10; i++)
                {
                    matrices[i] = float4x4.TRS(new float3(5, 0, 0), quaternion.identity, new float3(1f));
                    frustum[i] = i;
                }
                int visible = RunDistanceCull(matrices, frustum, 10, float3.zero, 100f); // maxDistSq = 100
                Assert.AreEqual(10, visible);
            }
            finally { matrices.Dispose(); frustum.Dispose(); }
        }

        [Test]
        public void AllOutsideThreshold_NonePass()
        {
            var matrices = new NativeArray<float4x4>(10, Allocator.TempJob);
            var frustum = new NativeArray<int>(10, Allocator.TempJob);
            try
            {
                for (int i = 0; i < 10; i++)
                {
                    matrices[i] = float4x4.TRS(new float3(50, 0, 0), quaternion.identity, new float3(1f));
                    frustum[i] = i;
                }
                int visible = RunDistanceCull(matrices, frustum, 10, float3.zero, 100f); // maxDistSq = 100
                Assert.AreEqual(0, visible);
            }
            finally { matrices.Dispose(); frustum.Dispose(); }
        }

        [Test]
        public void MixedField_OnlyClosePass()
        {
            // 5 close (dist 3) + 5 far (dist 30); maxDist = 10 → only 5 close pass.
            var matrices = new NativeArray<float4x4>(10, Allocator.TempJob);
            var frustum = new NativeArray<int>(10, Allocator.TempJob);
            try
            {
                for (int i = 0; i < 5; i++)
                    matrices[i] = float4x4.TRS(new float3(3, 0, 0), quaternion.identity, new float3(1f));
                for (int i = 5; i < 10; i++)
                    matrices[i] = float4x4.TRS(new float3(30, 0, 0), quaternion.identity, new float3(1f));
                for (int i = 0; i < 10; i++) frustum[i] = i;

                int visible = RunDistanceCull(matrices, frustum, 10, float3.zero, 100f); // maxDistSq = 100
                Assert.AreEqual(5, visible);
            }
            finally { matrices.Dispose(); frustum.Dispose(); }
        }

        [Test]
        public void ExactlyAtThreshold_IsIncluded()
        {
            // distance == maxDistance → distSq == maxDistSq → "<= maxDistSq" includes it.
            var matrices = new NativeArray<float4x4>(1, Allocator.TempJob);
            var frustum = new NativeArray<int>(1, Allocator.TempJob);
            try
            {
                matrices[0] = float4x4.TRS(new float3(10, 0, 0), quaternion.identity, new float3(1f));
                frustum[0] = 0;
                int visible = RunDistanceCull(matrices, frustum, 1, float3.zero, 100f); // dist²=100, max²=100
                Assert.AreEqual(1, visible);
            }
            finally { matrices.Dispose(); frustum.Dispose(); }
        }

        [Test]
        public void OffsetCameraPosition_DistanceMeasuredFromCamera()
        {
            // Camera at (100, 0, 0); instance at (105, 0, 0); maxDist = 10 → distance = 5 → passes.
            var matrices = new NativeArray<float4x4>(1, Allocator.TempJob);
            var frustum = new NativeArray<int>(1, Allocator.TempJob);
            try
            {
                matrices[0] = float4x4.TRS(new float3(105, 0, 0), quaternion.identity, new float3(1f));
                frustum[0] = 0;
                int visible = RunDistanceCull(matrices, frustum, 1, new float3(100, 0, 0), 100f);
                Assert.AreEqual(1, visible);
            }
            finally { matrices.Dispose(); frustum.Dispose(); }
        }
    }
}
