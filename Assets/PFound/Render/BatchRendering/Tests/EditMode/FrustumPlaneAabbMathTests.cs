using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using PFound.Render.BatchRendering;
// Disambiguate vs UnityEngine.FrustumPlanes (Unity's non-Burst built-in struct).
using FrustumPlanes = PFound.Render.BatchRendering.FrustumPlanes;

namespace PFound.Render.Tests.BatchRendering
{
    /// <summary>
    /// Covers FR-033 — frustum-vs-sphere cull math correctness. Schedules the real Burst job
    /// against synthesized instances + a known camera and asserts the visible-set matches hand
    /// expectations.
    /// </summary>
    public sealed class FrustumPlaneAabbMathTests
    {
        private static FrustumPlanes BuildPlanes(Camera cam)
        {
            FrustumPlanes.FromCamera(cam, new Plane[6], out var planes);
            return planes;
        }

        private static GameObject MakeCamera(Vector3 pos, Quaternion rot, float fov, float near, float far)
        {
            var go = new GameObject("__test_camera__");
            go.transform.position = pos;
            go.transform.rotation = rot;
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.nearClipPlane = near;
            cam.farClipPlane = far;
            cam.aspect = 16f / 9f;
            cam.orthographic = false;
            return go;
        }

        private static int RunCull(NativeArray<float4x4> matrices, FrustumPlanes planes, float3 meshCenter, float meshRadius)
        {
            var list = new NativeList<int>(matrices.Length, Allocator.TempJob);
            try
            {
                list.Capacity = matrices.Length;
                var job = new FrustumCullJob
                {
                    Matrices = matrices,
                    Planes = planes,
                    MeshLocalCenter = meshCenter,
                    MeshLocalRadius = meshRadius,
                    VisibleIndices = list.AsParallelWriter(),
                };
                job.Schedule(matrices.Length, 64).Complete();
                return list.Length;
            }
            finally { list.Dispose(); }
        }

        [Test]
        public void SphereAtOriginInsideForwardCamera_IsVisible()
        {
            var camGO = MakeCamera(new Vector3(0, 0, -10), Quaternion.identity, 60f, 0.3f, 1000f);
            var matrices = new NativeArray<float4x4>(1, Allocator.TempJob);
            try
            {
                var planes = BuildPlanes(camGO.GetComponent<Camera>());
                matrices[0] = float4x4.TRS(float3.zero, quaternion.identity, new float3(1f));
                int visible = RunCull(matrices, planes, float3.zero, 0.5f);
                Assert.AreEqual(1, visible);
            }
            finally
            {
                matrices.Dispose();
                Object.DestroyImmediate(camGO);
            }
        }

        [Test]
        public void SphereBehindCamera_IsCulled()
        {
            var camGO = MakeCamera(new Vector3(0, 0, -10), Quaternion.identity, 60f, 0.3f, 1000f);
            var matrices = new NativeArray<float4x4>(1, Allocator.TempJob);
            try
            {
                var planes = BuildPlanes(camGO.GetComponent<Camera>());
                // Behind the camera (camera at z=-10 looking toward +z, sphere at z=-100 is behind).
                matrices[0] = float4x4.TRS(new float3(0, 0, -100), quaternion.identity, new float3(1f));
                int visible = RunCull(matrices, planes, float3.zero, 0.5f);
                Assert.AreEqual(0, visible);
            }
            finally
            {
                matrices.Dispose();
                Object.DestroyImmediate(camGO);
            }
        }

        [Test]
        public void SphereBeyondFarPlane_IsCulled()
        {
            var camGO = MakeCamera(new Vector3(0, 0, -10), Quaternion.identity, 60f, 0.3f, 100f);
            var matrices = new NativeArray<float4x4>(1, Allocator.TempJob);
            try
            {
                var planes = BuildPlanes(camGO.GetComponent<Camera>());
                // Way beyond the far plane.
                matrices[0] = float4x4.TRS(new float3(0, 0, 500), quaternion.identity, new float3(1f));
                int visible = RunCull(matrices, planes, float3.zero, 0.5f);
                Assert.AreEqual(0, visible);
            }
            finally
            {
                matrices.Dispose();
                Object.DestroyImmediate(camGO);
            }
        }

        [Test]
        public void MixedField_VisibleAndCulled()
        {
            var camGO = MakeCamera(new Vector3(0, 0, -10), Quaternion.identity, 60f, 0.3f, 1000f);
            var matrices = new NativeArray<float4x4>(4, Allocator.TempJob);
            try
            {
                var planes = BuildPlanes(camGO.GetComponent<Camera>());
                matrices[0] = float4x4.TRS(new float3(0, 0, 0), quaternion.identity, new float3(1f));      // in front, visible
                matrices[1] = float4x4.TRS(new float3(0, 0, 50), quaternion.identity, new float3(1f));     // in front far, visible
                matrices[2] = float4x4.TRS(new float3(0, 0, -50), quaternion.identity, new float3(1f));    // behind, culled
                matrices[3] = float4x4.TRS(new float3(500, 0, 0), quaternion.identity, new float3(1f));    // far off-axis, culled
                int visible = RunCull(matrices, planes, float3.zero, 0.5f);
                Assert.AreEqual(2, visible);
            }
            finally
            {
                matrices.Dispose();
                Object.DestroyImmediate(camGO);
            }
        }

        [Test]
        public void LargeSphereAtCameraEdge_IsVisible_ConservativeBound()
        {
            // A sphere that barely touches the frustum plane should still be visible (conservative
            // cull: false positives draw — the test asserts the cull doesn't miss a borderline-in
            // instance).
            var camGO = MakeCamera(new Vector3(0, 0, -10), Quaternion.identity, 60f, 0.3f, 1000f);
            var matrices = new NativeArray<float4x4>(1, Allocator.TempJob);
            try
            {
                var planes = BuildPlanes(camGO.GetComponent<Camera>());
                matrices[0] = float4x4.TRS(new float3(10, 0, 0), quaternion.identity, new float3(1f));
                int visible = RunCull(matrices, planes, float3.zero, 10f);
                Assert.AreEqual(1, visible);
            }
            finally
            {
                matrices.Dispose();
                Object.DestroyImmediate(camGO);
            }
        }
    }
}
