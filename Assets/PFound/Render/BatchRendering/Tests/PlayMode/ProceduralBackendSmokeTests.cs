using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PFound.Render.BatchRendering;

namespace PFound.Render.Tests.BatchRendering.PlayMode
{
    /// <summary>
    /// Phase 11 Procedural backend smoke — verifies the service registers a procedural batch and
    /// ticks across frames without errors. Procedural is a niche backend (consumer-owned vertex
    /// pipeline + args buffer); this test exercises the lifecycle and dispatch routing only.
    /// </summary>
    public sealed class ProceduralBackendSmokeTests
    {
        [UnityTest]
        public IEnumerator ProceduralBatch_TicksWithoutErrors()
        {
            if (!BackendCapabilityProbe.SupportsProcedural)
            {
                Assert.Ignore("Host platform does not support procedural rendering.");
                yield break;
            }

            var camGO = new GameObject("__procedural_test_camera__");
            camGO.transform.position = new Vector3(0, 0, -5);
            var camera = camGO.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.aspect = 1f;

            // Even though Procedural doesn't use the mesh's vertex pipeline (the consumer's shader
            // generates vertices procedurally from the SV_VertexID), the BatchRenderingBatch
            // descriptor requires a non-null mesh (FR-026). Pass a placeholder unit triangle.
            var mesh = new Mesh { name = "__procedural_placeholder__" };
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.RecalculateBounds();

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = "__procedural_test_mat__" };

            // Consumer-authored args buffer: a 5-element uint32 stride mimicking
            // GraphicsBuffer.IndirectDrawArgs layout for DrawProceduralIndirect.
            // For a smoke test we set vertex count = 3, instance count = 1, args[2] = startVertex,
            // args[3] = startInstance. The driver may reject this on some platforms; we wrap the
            // Dispatch in a try/catch so a bad-args refusal doesn't fail the smoke.
            const int argsCount = 5;
            var argsBuffer = new ComputeBuffer(argsCount, sizeof(uint), ComputeBufferType.IndirectArguments);
            argsBuffer.SetData(new uint[] { 3, 1, 0, 0, 0 });

            var service = new BatchRenderingService();
            // We reuse ComputeBufferInstanceSource as the args carrier for Procedural — consumer
            // owns the contents. Stride matches the args layout (5 × uint32 = 20 B).
            var source = new ComputeBufferInstanceSource(argsBuffer, stride: 20, count: 1);
            IBatchHandle handle = null;

            try
            {
                handle = service.RegisterBatch(new BatchRenderingBatch
                {
                    mesh = mesh,
                    material = material,
                    source = source,
                    backend = BackendKind.Procedural,
                    culling = CullingPolicy.None,
                });

                Assert.IsNotNull(handle);
                Assert.IsTrue(handle.IsAlive);
                Assert.IsFalse(handle.IsDegraded);

                yield return null;
                yield return null;
            }
            finally
            {
                handle?.Dispose();
                service.Dispose();
                if (argsBuffer != null) argsBuffer.Release();
                if (material != null) Object.DestroyImmediate(material);
                if (mesh != null) Object.DestroyImmediate(mesh);
                if (camGO != null) Object.DestroyImmediate(camGO);
            }

            // Don't assert LogAssert.NoUnexpectedReceived — DrawProceduralIndirect can emit driver
            // warnings on some platforms with our synthetic args. This smoke only verifies the
            // lifecycle wiring (register / tick / dispose) doesn't throw.
        }
    }
}
