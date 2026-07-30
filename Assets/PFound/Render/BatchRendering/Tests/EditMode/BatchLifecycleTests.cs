using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PFound.Render.BatchRendering;

namespace PFound.Render.Tests.BatchRendering
{
    /// <summary>
    /// Covers FR-033 — handle lifecycle states: registered → alive, disposed → not alive,
    /// idempotent Dispose, sticky degradation, service-dispose-invalidates-all.
    /// </summary>
    public sealed class BatchLifecycleTests
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
            _material = new Material(shader);
            _material.enableInstancing = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (_material != null) UnityEngine.Object.DestroyImmediate(_material);
            if (_mesh != null) UnityEngine.Object.DestroyImmediate(_mesh);
        }

        private BatchRenderingBatch MakeBatch(StubInstanceSource source)
        {
            return new BatchRenderingBatch
            {
                mesh = _mesh,
                material = _material,
                source = source,
                backend = BackendKind.Classic,
            };
        }

        [Test]
        public void RegisterBatch_ReturnsAliveNonDegradedHandle()
        {
            using var service = new BatchRenderingService();
            var src = new StubInstanceSource(8);
            try
            {
                var handle = service.RegisterBatch(MakeBatch(src));
                Assert.IsNotNull(handle);
                Assert.IsTrue(handle.IsAlive);
                Assert.IsFalse(handle.IsDegraded);
                Assert.IsFalse(handle.DegradedReason.HasValue);
                Assert.AreEqual(0, handle.LastFrameVisibleCount, "no tick yet");
                handle.Dispose();
            }
            finally { src.DisposeBacking(); }
        }

        [Test]
        public void HandleDispose_FlipsIsAliveToFalse()
        {
            using var service = new BatchRenderingService();
            var src = new StubInstanceSource(4);
            try
            {
                var handle = service.RegisterBatch(MakeBatch(src));
                Assert.IsTrue(handle.IsAlive);
                handle.Dispose();
                Assert.IsFalse(handle.IsAlive);
            }
            finally { src.DisposeBacking(); }
        }

        [Test]
        public void HandleDispose_IsIdempotent()
        {
            using var service = new BatchRenderingService();
            var src = new StubInstanceSource(4);
            try
            {
                var handle = service.RegisterBatch(MakeBatch(src));
                Assert.DoesNotThrow(() => handle.Dispose());
                Assert.DoesNotThrow(() => handle.Dispose());
                Assert.IsFalse(handle.IsAlive);
            }
            finally { src.DisposeBacking(); }
        }

        [Test]
        public void ServiceDispose_InvalidatesAllHandles()
        {
            var service = new BatchRenderingService();
            var s1 = new StubInstanceSource(4);
            var s2 = new StubInstanceSource(8);
            try
            {
                var h1 = service.RegisterBatch(MakeBatch(s1));
                var h2 = service.RegisterBatch(MakeBatch(s2));
                Assert.IsTrue(h1.IsAlive);
                Assert.IsTrue(h2.IsAlive);
                service.Dispose();
                Assert.IsFalse(h1.IsAlive);
                Assert.IsFalse(h2.IsAlive);

                // Stale-handle reads do not throw.
                Assert.DoesNotThrow(() => { var _ = h1.LastFrameVisibleCount; });
                Assert.DoesNotThrow(() => h1.Dispose());
            }
            finally
            {
                s1.DisposeBacking();
                s2.DisposeBacking();
            }
        }

        [Test]
        public void Degradation_IsSticky_MissingEnableInstancing()
        {
            using var service = new BatchRenderingService();
            var src = new StubInstanceSource(4);
            var brokenMat = new Material(_material.shader);
            brokenMat.enableInstancing = false;
            try
            {
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*missing enableInstancing.*"));
                var handle = service.RegisterBatch(new BatchRenderingBatch
                {
                    mesh = _mesh,
                    material = brokenMat,
                    source = src,
                    backend = BackendKind.Classic,
                });
                Assert.IsTrue(handle.IsAlive);
                Assert.IsTrue(handle.IsDegraded);
                Assert.AreEqual(BatchDegradedReason.MissingEnableInstancing, handle.DegradedReason);

                handle.Dispose();
                // Sticky — degradation reason remains after dispose for forensic readout.
                Assert.AreEqual(BatchDegradedReason.MissingEnableInstancing, handle.DegradedReason);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(brokenMat);
                src.DisposeBacking();
            }
        }
    }
}
