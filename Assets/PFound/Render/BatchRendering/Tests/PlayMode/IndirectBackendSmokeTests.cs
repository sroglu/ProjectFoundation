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
    /// Phase 11 US2 smoke — registers a <see cref="BackendKind.Indirect"/> batch backed by a
    /// <see cref="ComputeBuffer"/> of <see cref="MeshInstanceData"/> and verifies the service ticks
    /// across frames without errors. Indirect backend gracefully degrades on unsupported platforms
    /// (test ignores in that case).
    /// </summary>
    public sealed class IndirectBackendSmokeTests
    {
        [UnityTest]
        public IEnumerator IndirectBatch_TicksAcrossFrames_ReportsVisibleCount()
        {
            if (!BackendCapabilityProbe.SupportsIndirect)
            {
                Assert.Ignore("Host platform does not support indirect rendering — skipping smoke test.");
                yield break;
            }

            var camGO = new GameObject("__indirect_test_camera__");
            camGO.transform.position = new Vector3(0, 0, -10);
            var camera = camGO.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1000f;
            camera.aspect = 1f;

            var mesh = CreateUnitCubeMesh();
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = "__indirect_test_material__", enableInstancing = true };

            const int instanceCount = 64;
            var buffer = new ComputeBuffer(instanceCount, ComputeBufferInstanceSource.MeshInstanceDataStride);
            var hostData = new MeshInstanceData[instanceCount];
            for (int i = 0; i < instanceCount; i++)
            {
                hostData[i] = new MeshInstanceData
                {
                    LocalToWorld = float4x4.TRS(new float3(i * 0.5f - 16f, 0, 5f), quaternion.identity, new float3(0.3f)),
                    PerInstanceColor = new float4(1, 1, 1, 1),
                };
            }
            buffer.SetData(hostData);

            var service = new BatchRenderingService();
            var source = new ComputeBufferInstanceSource(buffer, ComputeBufferInstanceSource.MeshInstanceDataStride);
            IBatchHandle handle = null;

            try
            {
                handle = service.RegisterBatch(new BatchRenderingBatch
                {
                    mesh = mesh,
                    material = material,
                    source = source,
                    backend = BackendKind.Indirect,
                    // GPU-side data — consumer fully owns culling.
                    culling = CullingPolicy.None,
                });

                Assert.IsNotNull(handle);
                Assert.IsTrue(handle.IsAlive);
                Assert.IsFalse(handle.IsDegraded);

                // Drive the service tick directly. EditMode test framework does NOT fire
                // PlayerLoop BeforeRender; call OnBeforeRender so the cull + dispatch runs.
                service.OnBeforeRender();
                yield return null;

                Assert.AreEqual(instanceCount, handle.LastFrameVisibleCount);
            }
            finally
            {
                handle?.Dispose();
                service.Dispose();
                if (buffer != null) buffer.Release();
                if (material != null) Object.DestroyImmediate(material);
                if (mesh != null) Object.DestroyImmediate(mesh);
                if (camGO != null) Object.DestroyImmediate(camGO);
            }

        }

        [UnityTest]
        public IEnumerator IndirectBatch_OnUnsupportedPlatform_IsDegraded()
        {
            // If the platform does support indirect, this test is a no-op — the FR-023a path is
            // exercised on unsupported hosts (WebGL 2, older Android) which we can't simulate from
            // EditMode tests on a desktop. We DO assert the degraded-flag plumbing works correctly
            // by directly degrading through the unsupported path — but only if we're on a host
            // that genuinely lacks support.
            if (BackendCapabilityProbe.SupportsIndirect)
            {
                Assert.Ignore("Host supports indirect — FR-023a degraded path not reachable on this platform.");
                yield break;
            }

            var mesh = CreateUnitCubeMesh();
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = "__indirect_unsup_mat__", enableInstancing = true };

            const int instanceCount = 4;
            var buffer = new ComputeBuffer(instanceCount, ComputeBufferInstanceSource.MeshInstanceDataStride);

            var service = new BatchRenderingService();
            var source = new ComputeBufferInstanceSource(buffer, ComputeBufferInstanceSource.MeshInstanceDataStride);

            try
            {
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*lacks .*"));
                var handle = service.RegisterBatch(new BatchRenderingBatch
                {
                    mesh = mesh,
                    material = material,
                    source = source,
                    backend = BackendKind.Indirect,
                    culling = CullingPolicy.None,
                });
                Assert.IsTrue(handle.IsDegraded);
                Assert.AreEqual(BatchDegradedReason.BackendUnsupported, handle.DegradedReason);
                handle.Dispose();
            }
            finally
            {
                service.Dispose();
                buffer.Release();
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(mesh);
            }

            yield return null;
        }

        private static Mesh CreateUnitCubeMesh()
        {
            var mesh = new Mesh { name = "__indirect_unit_cube__" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f),
            };
            mesh.triangles = new[]
            {
                0, 2, 1, 0, 3, 2,
                4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4,
                3, 7, 6, 3, 6, 2,
                0, 4, 7, 0, 7, 3,
                1, 2, 6, 1, 6, 5,
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
