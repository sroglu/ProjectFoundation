using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PFound.Render.BatchRendering;

namespace PFound.Render.Tests.BatchRendering
{
    /// <summary>
    /// Covers FR-023 + SC-008 — registering a <see cref="BackendKind.Classic"/> batch with
    /// <c>material.enableInstancing == false</c> degrades the batch with a one-shot warning that
    /// names the offending material.
    /// </summary>
    public sealed class MissingEnableInstancingTests
    {
        private Mesh _mesh;
        private Material _matNoInstancing;
        private Material _matWithInstancing;

        [SetUp]
        public void SetUp()
        {
            _mesh = new Mesh();
            _mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            _mesh.triangles = new[] { 0, 1, 2 };
            _mesh.RecalculateBounds();

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _matNoInstancing = new Material(shader) { name = "TestMat_NoInstancing", enableInstancing = false };
            _matWithInstancing = new Material(shader) { name = "TestMat_WithInstancing", enableInstancing = true };
        }

        [TearDown]
        public void TearDown()
        {
            if (_matNoInstancing != null) UnityEngine.Object.DestroyImmediate(_matNoInstancing);
            if (_matWithInstancing != null) UnityEngine.Object.DestroyImmediate(_matWithInstancing);
            if (_mesh != null) UnityEngine.Object.DestroyImmediate(_mesh);
        }

        [Test]
        public void ClassicBackend_MissingEnableInstancing_DegradesHandle()
        {
            using var service = new BatchRenderingService();
            var src = new StubInstanceSource(4);
            try
            {
                LogAssert.Expect(LogType.Warning, new Regex(".*missing enableInstancing.*"));
                var handle = service.RegisterBatch(new BatchRenderingBatch
                {
                    mesh = _mesh,
                    material = _matNoInstancing,
                    source = src,
                    backend = BackendKind.Classic,
                });
                Assert.IsTrue(handle.IsAlive);
                Assert.IsTrue(handle.IsDegraded);
                Assert.AreEqual(BatchDegradedReason.MissingEnableInstancing, handle.DegradedReason);
                handle.Dispose();
            }
            finally { src.DisposeBacking(); }
        }

        [Test]
        public void ClassicBackend_MissingEnableInstancing_WarningMentionsMaterialName()
        {
            using var service = new BatchRenderingService();
            var src = new StubInstanceSource(4);
            try
            {
                // SC-008: the one-shot warning identifies the offending material by name.
                LogAssert.Expect(LogType.Warning, new Regex(".*TestMat_NoInstancing.*"));
                var handle = service.RegisterBatch(new BatchRenderingBatch
                {
                    mesh = _mesh,
                    material = _matNoInstancing,
                    source = src,
                    backend = BackendKind.Classic,
                });
                handle.Dispose();
            }
            finally { src.DisposeBacking(); }
        }

        [Test]
        public void ClassicBackend_EnableInstancingTrue_NoWarning_HealthyHandle()
        {
            using var service = new BatchRenderingService();
            var src = new StubInstanceSource(4);
            try
            {
                var handle = service.RegisterBatch(new BatchRenderingBatch
                {
                    mesh = _mesh,
                    material = _matWithInstancing,
                    source = src,
                    backend = BackendKind.Classic,
                });
                Assert.IsFalse(handle.IsDegraded);
                Assert.IsFalse(handle.DegradedReason.HasValue);
                handle.Dispose();
            }
            finally { src.DisposeBacking(); }
        }
    }
}
