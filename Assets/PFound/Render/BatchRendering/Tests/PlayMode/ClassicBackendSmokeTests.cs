using System.Collections;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using PFound.Render.BatchRendering;

namespace PFound.Render.Tests.BatchRendering.PlayMode
{
    /// <summary>
    /// Phase 11 US1 smoke — verifies a Classic-instanced batch ticks across multiple frames in
    /// PlayMode without errors and reports a non-zero <see cref="IBatchHandle.LastFrameVisibleCount"/>
    /// when instances are placed inside the camera frustum.
    /// </summary>
    /// <remarks>
    /// FR-034 also requires a draw-call-count assertion via Frame Debugger; that path requires
    /// editor-only Frame Debugger API and is verified manually as part of the Phase 11 ship
    /// (verification.md). This test pins the visible-count semantics + lifecycle.
    /// </remarks>
    public sealed class ClassicBackendSmokeTests
    {
        private const int InstanceCount = 100;
        private const float FieldExtent = 5f;

        [UnityTest]
        public IEnumerator ClassicBatch_TicksAcrossFrames_ReportsVisibleCount()
        {
            // Scene setup.
            var camGO = new GameObject("__test_camera__");
            camGO.transform.position = new Vector3(0, 0, -10);
            var camera = camGO.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1000f;
            camera.aspect = 1f;

            var mesh = CreateUnitCubeMesh();
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = "__test_material__", enableInstancing = true };

            // 100 cubes in a 5×5 unit area in front of the camera.
            var transforms = new NativeArray<float4x4>(InstanceCount, Allocator.Persistent);
            for (int i = 0; i < InstanceCount; i++)
            {
                float x = ((i % 10) / 9f - 0.5f) * FieldExtent * 2f;
                float y = ((i / 10) / 9f - 0.5f) * FieldExtent * 2f;
                float z = 5f;
                transforms[i] = float4x4.TRS(new float3(x, y, z), quaternion.identity, new float3(0.3f));
            }

            var service = new BatchRenderingService();
            var source = new NativeArrayInstanceSource(transforms);
            IBatchHandle handle = null;

            try
            {
                handle = service.RegisterBatch(new BatchRenderingBatch
                {
                    mesh = mesh,
                    material = material,
                    source = source,
                    backend = BackendKind.Classic,
                    culling = CullingPolicy.Default,
                });

                Assert.IsNotNull(handle);
                Assert.IsTrue(handle.IsAlive);
                Assert.IsFalse(handle.IsDegraded, "Handle should not be degraded; descriptor is well-formed.");

                // Pre-tick state.
                Assert.AreEqual(0, handle.LastFrameVisibleCount);

                // Drive the service tick directly. In EditMode tests Unity's PlayerLoop (which
                // drives PFound.LoopScheduler's BeforeRender callback) does NOT fire — the editor
                // frame only ticks EditorApplication.update. Call OnBeforeRender so the batch
                // gets processed deterministically.
                service.OnBeforeRender();
                yield return null;

                // Post-tick: visible count should be > 0 (all 100 cubes are in front of the camera).
                Assert.Greater(handle.LastFrameVisibleCount, 0, "Expected visible cubes in the camera frustum.");
                Assert.LessOrEqual(handle.LastFrameVisibleCount, InstanceCount, "Visible count cannot exceed total.");
            }
            finally
            {
                handle?.Dispose();
                service.Dispose();
                if (transforms.IsCreated) transforms.Dispose();
                if (material != null) Object.DestroyImmediate(material);
                if (mesh != null) Object.DestroyImmediate(mesh);
                if (camGO != null) Object.DestroyImmediate(camGO);
            }

        }

        [UnityTest]
        public IEnumerator ClassicBatch_CullingNoneRendersAllInstances()
        {
            var camGO = new GameObject("__test_camera_none__");
            camGO.transform.position = new Vector3(0, 0, -10);
            var camera = camGO.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1000f;
            camera.aspect = 1f;

            var mesh = CreateUnitCubeMesh();
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = "__test_material_none__", enableInstancing = true };

            var transforms = new NativeArray<float4x4>(50, Allocator.Persistent);
            for (int i = 0; i < 50; i++)
            {
                // Half the instances are way behind the camera — should still all render with None.
                float z = (i < 25) ? 5f : -500f;
                transforms[i] = float4x4.TRS(new float3(i * 0.1f, 0, z), quaternion.identity, new float3(0.3f));
            }

            var service = new BatchRenderingService();
            var source = new NativeArrayInstanceSource(transforms);
            IBatchHandle handle = null;

            try
            {
                handle = service.RegisterBatch(new BatchRenderingBatch
                {
                    mesh = mesh,
                    material = material,
                    source = source,
                    backend = BackendKind.Classic,
                    culling = CullingPolicy.None,
                });
                service.OnBeforeRender();
                yield return null;

                Assert.AreEqual(50, handle.LastFrameVisibleCount, "CullingPolicy.None should pass through all 50 instances.");
            }
            finally
            {
                handle?.Dispose();
                service.Dispose();
                if (transforms.IsCreated) transforms.Dispose();
                if (material != null) Object.DestroyImmediate(material);
                if (mesh != null) Object.DestroyImmediate(mesh);
                if (camGO != null) Object.DestroyImmediate(camGO);
            }

        }

        [UnityTest]
        public IEnumerator ClassicBatch_DisposeStopsTicking()
        {
            var camGO = new GameObject("__test_camera_dispose__");
            camGO.transform.position = new Vector3(0, 0, -10);
            var camera = camGO.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1000f;
            camera.aspect = 1f;

            var mesh = CreateUnitCubeMesh();
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = "__test_material_dispose__", enableInstancing = true };

            var transforms = new NativeArray<float4x4>(10, Allocator.Persistent);
            for (int i = 0; i < 10; i++)
                transforms[i] = float4x4.TRS(new float3(i * 0.5f, 0, 5), quaternion.identity, new float3(0.3f));

            var service = new BatchRenderingService();
            var source = new NativeArrayInstanceSource(transforms);

            try
            {
                var handle = service.RegisterBatch(new BatchRenderingBatch
                {
                    mesh = mesh,
                    material = material,
                    source = source,
                    backend = BackendKind.Classic,
                });
                yield return null;
                Assert.IsTrue(handle.IsAlive);

                handle.Dispose();
                Assert.IsFalse(handle.IsAlive);

                // Tick another frame — should not crash, should not draw the disposed batch.
                yield return null;
            }
            finally
            {
                service.Dispose();
                if (transforms.IsCreated) transforms.Dispose();
                if (material != null) Object.DestroyImmediate(material);
                if (mesh != null) Object.DestroyImmediate(mesh);
                if (camGO != null) Object.DestroyImmediate(camGO);
            }

        }

        // ---------------- Helpers ----------------

        private static Mesh CreateUnitCubeMesh()
        {
            // Inline unit cube — avoids depending on Resources.GetBuiltinResource which can vary
            // across Unity setups.
            var mesh = new Mesh { name = "__test_unit_cube__" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f),
            };
            mesh.triangles = new[]
            {
                0, 2, 1, 0, 3, 2, // back
                4, 5, 6, 4, 6, 7, // front
                0, 1, 5, 0, 5, 4, // bottom
                3, 7, 6, 3, 6, 2, // top
                0, 4, 7, 0, 7, 3, // left
                1, 2, 6, 1, 6, 5, // right
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
