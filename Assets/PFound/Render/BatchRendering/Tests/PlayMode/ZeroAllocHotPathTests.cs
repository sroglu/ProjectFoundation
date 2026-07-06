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
    /// Covers FR-027 + SC-002 + SC-003 + SC-009 — zero per-frame managed allocations on the
    /// cull-and-dispatch hot path. The service ticks via LoopScheduler BeforeRender naturally; the
    /// profiler recorder samples the per-frame GC.Alloc total.
    /// </summary>
    public sealed class ZeroAllocHotPathTests
    {
        [UnityTest]
        public IEnumerator ClassicBackend_5000Instances_ZeroAlloc()
        {
            var camGO = new GameObject("__zeroalloc_classic_camera__");
            camGO.transform.position = new Vector3(0, 0, -10);
            var camera = camGO.AddComponent<Camera>();
            camera.fieldOfView = 60f;

            var mesh = CreateUnitCubeMesh();
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = "__zeroalloc_classic_mat__", enableInstancing = true };

            const int count = 5000;
            var transforms = new NativeArray<float4x4>(count, Allocator.Persistent);
            for (int i = 0; i < count; i++)
            {
                float x = (i % 71) * 0.5f - 17f;
                float y = ((i / 71) % 71) * 0.5f - 17f;
                transforms[i] = float4x4.TRS(new float3(x, y, 5f), quaternion.identity, new float3(0.2f));
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

                // Warm up — drive the service tick a few times so first-time Burst compile / pool
                // growth / scratch allocs don't pollute the measurement window.
                for (int i = 0; i < 4; i++) { service.OnBeforeRender(); yield return null; }

                yield return ZeroAllocAssertions.AssertZeroAlloc(
                    action: () => service.OnBeforeRender(),
                    frameCount: 60,
                    label: "Classic backend cull+dispatch (5000 instances)");
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
        public IEnumerator IndirectBackend_50000Instances_ZeroAlloc()
        {
            if (!BackendCapabilityProbe.SupportsIndirect)
            {
                Assert.Ignore("Host platform does not support indirect rendering — skipping SC-003 test.");
                yield break;
            }

            var camGO = new GameObject("__zeroalloc_indirect_camera__");
            camGO.transform.position = new Vector3(0, 0, -10);
            var camera = camGO.AddComponent<Camera>();
            camera.fieldOfView = 60f;

            var mesh = CreateUnitCubeMesh();
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = "__zeroalloc_indirect_mat__", enableInstancing = true };

            const int count = 50_000;
            var buffer = new ComputeBuffer(count, ComputeBufferInstanceSource.MeshInstanceDataStride);
            var hostData = new MeshInstanceData[count];
            for (int i = 0; i < count; i++)
            {
                hostData[i] = new MeshInstanceData
                {
                    LocalToWorld = float4x4.TRS(new float3((i % 224) * 0.1f - 11f, (i / 224) * 0.1f - 11f, 5f), quaternion.identity, new float3(0.1f)),
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
                    culling = CullingPolicy.None,
                });

                for (int i = 0; i < 4; i++) { service.OnBeforeRender(); yield return null; }

                yield return ZeroAllocAssertions.AssertZeroAlloc(
                    action: () => service.OnBeforeRender(),
                    frameCount: 60,
                    label: "Indirect backend cull+dispatch (50000 instances)");
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

        private static Mesh CreateUnitCubeMesh()
        {
            var mesh = new Mesh { name = "__zeroalloc_unit_cube__" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f),
            };
            mesh.triangles = new[]
            {
                0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4, 3, 7, 6, 3, 6, 2,
                0, 4, 7, 0, 7, 3, 1, 2, 6, 1, 6, 5,
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
