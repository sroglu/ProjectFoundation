using System.Text.RegularExpressions;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PFound.Render.BatchRendering;

namespace PFound.Render.Tests.BatchRendering
{
    /// <summary>
    /// Covers FR-018 (occlusion stub), FR-024 (zero-count first-seen), and the owner-managed
    /// contract violation paths (FR detected on tick when mesh / material is destroyed externally).
    /// </summary>
    public sealed class EdgeCaseDiagnosticsTests
    {
        private Mesh _mesh;
        private Material _material;

        [SetUp]
        public void SetUp()
        {
            _mesh = new Mesh();
            _mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            _mesh.triangles = new[] { 0, 1, 2 };
            _mesh.RecalculateBounds();
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _material = new Material(shader) { name = "__edge_case_mat__", enableInstancing = true };
        }

        [TearDown]
        public void TearDown()
        {
            if (_material != null) Object.DestroyImmediate(_material);
            if (_mesh != null) Object.DestroyImmediate(_mesh);
        }

        [Test]
        public void OcclusionFlag_EmitsOneShot_StubWarning()
        {
            using var service = new BatchRenderingService();
            var src = new StubInstanceSource(4);
            try
            {
                LogAssert.Expect(LogType.Warning, new Regex(".*occlusion culling is not implemented.*"));
                var handle = service.RegisterBatch(new BatchRenderingBatch
                {
                    mesh = _mesh,
                    material = _material,
                    source = src,
                    backend = BackendKind.Classic,
                    culling = new CullingPolicy
                    {
                        frustum = true,
                        distance = default,
                        occlusion = true,
                    },
                });

                // Occlusion alone doesn't flip IsDegraded; reason is set for diagnostic readout.
                Assert.AreEqual(BatchDegradedReason.OcclusionStubActive, handle.DegradedReason);
                Assert.IsFalse(handle.IsDegraded, "Occlusion stub alone does not mark the batch degraded.");
                handle.Dispose();
            }
            finally { src.DisposeBacking(); }
        }

        [UnityTest]
        public IEnumerator MeshDestroyedExternally_OnTick_TransitionsToDegraded()
        {
            var service = new BatchRenderingService();
            var src = new StubInstanceSource(4);
            try
            {
                var handle = service.RegisterBatch(new BatchRenderingBatch
                {
                    mesh = _mesh,
                    material = _material,
                    source = src,
                    backend = BackendKind.Classic,
                });

                Assert.IsFalse(handle.IsDegraded);

                // Destroy the mesh while the batch is still registered (owner-managed contract
                // violation). Service should detect on next tick + emit one-shot warning + degrade.
                LogAssert.Expect(LogType.Warning, new Regex(".*Mesh was destroyed.*"));
                Object.DestroyImmediate(_mesh);
                _mesh = null;

                // EditMode test framework does not drive PlayerLoop BeforeRender; tick directly.
                service.OnBeforeRender();
                yield return null;

                Assert.IsTrue(handle.IsDegraded);
                Assert.AreEqual(BatchDegradedReason.MeshDestroyed, handle.DegradedReason);
                handle.Dispose();
            }
            finally
            {
                service.Dispose();
                src.DisposeBacking();
            }
        }

        [UnityTest]
        public IEnumerator ZeroCountSource_FirstSeen_LogsInfo()
        {
            var service = new BatchRenderingService();
            var src = new StubInstanceSource(0);
            try
            {
                var handle = service.RegisterBatch(new BatchRenderingBatch
                {
                    mesh = _mesh,
                    material = _material,
                    source = src,
                    backend = BackendKind.Classic,
                });

                LogAssert.Expect(LogType.Log, new Regex(".*Count == 0.*"));

                // EditMode test framework does not drive PlayerLoop BeforeRender; tick directly.
                service.OnBeforeRender();
                yield return null;
                // Second tick — should NOT emit again (one-shot gating).
                service.OnBeforeRender();
                yield return null;

                Assert.IsFalse(handle.IsDegraded);
                handle.Dispose();
            }
            finally
            {
                service.Dispose();
                src.DisposeBacking();
            }
        }
    }
}
