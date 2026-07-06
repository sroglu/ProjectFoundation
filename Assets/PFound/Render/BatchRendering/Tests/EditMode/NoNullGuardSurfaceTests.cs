using System;
using NUnit.Framework;
using UnityEngine;
using PFound.Render.BatchRendering;

namespace PFound.Render.Tests.BatchRendering
{
    /// <summary>
    /// Covers FR-026 / FR-033 — every public API on <see cref="BatchRenderingService"/> rejects null
    /// with <see cref="ArgumentNullException"/>; subMesh range validated; post-dispose throws
    /// <see cref="ObjectDisposedException"/>.
    /// </summary>
    public sealed class NoNullGuardSurfaceTests
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

            // Use a stand-in shader that exists in any URP project.
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

        [Test]
        public void RegisterBatch_NullMesh_Throws()
        {
            using var service = new BatchRenderingService();
            var src = new StubInstanceSource(4);
            try
            {
                Assert.Throws<ArgumentNullException>(() => service.RegisterBatch(new BatchRenderingBatch
                {
                    mesh = null,
                    material = _material,
                    source = src,
                    backend = BackendKind.Classic,
                }));
            }
            finally { src.DisposeBacking(); }
        }

        [Test]
        public void RegisterBatch_NullMaterial_Throws()
        {
            using var service = new BatchRenderingService();
            var src = new StubInstanceSource(4);
            try
            {
                Assert.Throws<ArgumentNullException>(() => service.RegisterBatch(new BatchRenderingBatch
                {
                    mesh = _mesh,
                    material = null,
                    source = src,
                    backend = BackendKind.Classic,
                }));
            }
            finally { src.DisposeBacking(); }
        }

        [Test]
        public void RegisterBatch_NullSource_Throws()
        {
            using var service = new BatchRenderingService();
            Assert.Throws<ArgumentNullException>(() => service.RegisterBatch(new BatchRenderingBatch
            {
                mesh = _mesh,
                material = _material,
                source = null,
                backend = BackendKind.Classic,
            }));
        }

        [Test]
        public void RegisterBatch_SubMeshIndexOutOfRange_Throws()
        {
            using var service = new BatchRenderingService();
            var src = new StubInstanceSource(4);
            try
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => service.RegisterBatch(new BatchRenderingBatch
                {
                    mesh = _mesh,
                    material = _material,
                    source = src,
                    subMeshIndex = 99,
                    backend = BackendKind.Classic,
                }));

                Assert.Throws<ArgumentOutOfRangeException>(() => service.RegisterBatch(new BatchRenderingBatch
                {
                    mesh = _mesh,
                    material = _material,
                    source = src,
                    subMeshIndex = -1,
                    backend = BackendKind.Classic,
                }));
            }
            finally { src.DisposeBacking(); }
        }

        [Test]
        public void RegisterBatch_AfterDispose_Throws()
        {
            var service = new BatchRenderingService();
            service.Dispose();
            var src = new StubInstanceSource(4);
            try
            {
                Assert.Throws<ObjectDisposedException>(() => service.RegisterBatch(new BatchRenderingBatch
                {
                    mesh = _mesh,
                    material = _material,
                    source = src,
                    backend = BackendKind.Classic,
                }));
            }
            finally { src.DisposeBacking(); }
        }

        [Test]
        public void Dispose_IsIdempotent()
        {
            var service = new BatchRenderingService();
            service.Dispose();
            Assert.DoesNotThrow(() => service.Dispose());
        }
    }
}
