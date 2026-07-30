using System;
using NUnit.Framework;
using UnityEngine;
using PFound.Render.RenderContext;

namespace PFound.Render.Tests
{
    public sealed class MeshRendererAnchorTests
    {
        private GameObject _go;
        private MeshRenderer _mr;

        [SetUp]
        public void Setup()
        {
            _go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _mr = _go.GetComponent<MeshRenderer>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
        }

        [Test]
        public void Ctor_NullTarget_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new MeshRendererAnchor(null));
        }

        [Test]
        public void Target_ReturnsMeshRenderer()
        {
            var anchor = new MeshRendererAnchor(_mr);
            Assert.AreSame(_mr, anchor.Target);
        }

        [Test]
        public void TargetAlive_TrueWhenAttached()
        {
            var anchor = new MeshRendererAnchor(_mr);
            Assert.IsTrue(anchor.TargetAlive);
        }

        [Test]
        public void TargetAlive_FalseAfterDestroy()
        {
            var anchor = new MeshRendererAnchor(_mr);
            UnityEngine.Object.DestroyImmediate(_go);
            _go = null;
            Assert.IsFalse(anchor.TargetAlive);
        }

        [Test]
        public void ImplementsExplicitSizeAnchorMarker()
        {
            var anchor = new MeshRendererAnchor(_mr);
            // Marker enforces non-zero descriptor at service.Acquire; verified there in service tests.
            Assert.IsTrue(anchor is IExplicitSizeAnchor);
        }

        [Test]
        public void CreateSink_ReturnsMeshRendererSink()
        {
            var anchor = new MeshRendererAnchor(_mr);
            var sink = anchor.CreateSink();
            Assert.IsInstanceOf<MeshRendererSink>(sink);
        }

        [Test]
        public void Service_AcquireWithZeroDescriptor_Throws()
        {
            var anchor = new MeshRendererAnchor(_mr);
            var svc = new RenderContextService();
            try
            {
                var desc = RenderContextDescriptor.Default; // Width=0, Height=0
                Assert.Throws<ArgumentException>(() => svc.Acquire(desc, anchor));
            }
            finally { svc.Dispose(); }
        }
    }
}
