using System;
using NUnit.Framework;
using UnityEngine;
using PFound.Render.RenderContext;

namespace PFound.Render.Tests
{
    public sealed class MeshRendererSinkTests
    {
        private GameObject _go;
        private MeshRenderer _mr;
        private Material _sharedMat;
        private RenderTexture _rt;

        [SetUp]
        public void Setup()
        {
            _go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _mr = _go.GetComponent<MeshRenderer>();
            _sharedMat = _mr.sharedMaterial;
            _rt = new RenderTexture(64, 64, 0);
            _rt.Create();
        }

        [TearDown]
        public void Teardown()
        {
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
            if (_rt != null) { _rt.Release(); UnityEngine.Object.DestroyImmediate(_rt); }
        }

        [Test]
        public void Ctor_NullTarget_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new MeshRendererSink(null));
        }

        [Test]
        public void Bind_CreatesCloneAndAssignsRtToMainTex()
        {
            var sink = new MeshRendererSink(_mr);
            sink.Bind(_rt);
            var runtimeMat = _mr.sharedMaterial;
            Assert.AreNotSame(_sharedMat, runtimeMat, "Bind must clone the shared material");
            // The clone should reference _rt on _MainTex or _BaseMap
            bool refsRt =
                (runtimeMat.HasProperty("_MainTex") && runtimeMat.GetTexture("_MainTex") == _rt) ||
                (runtimeMat.HasProperty("_BaseMap") && runtimeMat.GetTexture("_BaseMap") == _rt);
            Assert.IsTrue(refsRt, "Clone material must reference the bound RT on _MainTex or _BaseMap");
        }

        [Test]
        public void Unbind_RestoresOriginalSharedMaterial()
        {
            var sink = new MeshRendererSink(_mr);
            sink.Bind(_rt);
            sink.Unbind();
            Assert.AreSame(_sharedMat, _mr.sharedMaterial, "Unbind must restore the original sharedMaterial");
        }

        [Test]
        public void Unbind_WithoutBind_IsNoOp()
        {
            var sink = new MeshRendererSink(_mr);
            Assert.DoesNotThrow(sink.Unbind);
            Assert.AreSame(_sharedMat, _mr.sharedMaterial);
        }

        [Test]
        public void DoubleUnbind_IsNoOp()
        {
            var sink = new MeshRendererSink(_mr);
            sink.Bind(_rt);
            sink.Unbind();
            Assert.DoesNotThrow(sink.Unbind);
        }
    }
}
