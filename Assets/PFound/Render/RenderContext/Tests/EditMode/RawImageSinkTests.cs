using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using PFound.Render.RenderContext;

namespace PFound.Render.Tests
{
    public sealed class RawImageSinkTests
    {
        private GameObject _go;
        private RawImage _rawImage;
        private Texture2D _priorTexture;
        private RenderTexture _rt;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("__raw", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            _rawImage = _go.GetComponent<RawImage>();
            _priorTexture = new Texture2D(4, 4);
            _rawImage.texture = _priorTexture;
            _rt = new RenderTexture(64, 64, 0);
            _rt.Create();
        }

        [TearDown]
        public void Teardown()
        {
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
            if (_priorTexture != null) UnityEngine.Object.DestroyImmediate(_priorTexture);
            if (_rt != null) { _rt.Release(); UnityEngine.Object.DestroyImmediate(_rt); }
        }

        [Test]
        public void Ctor_NullTarget_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new RawImageSink(null));
        }

        [Test]
        public void Bind_AssignsRenderTextureToTarget()
        {
            var sink = new RawImageSink(_rawImage);
            sink.Bind(_rt);
            Assert.AreSame(_rt, _rawImage.texture);
        }

        [Test]
        public void Unbind_RestoresCapturedTexture()
        {
            var sink = new RawImageSink(_rawImage);
            sink.Bind(_rt);
            sink.Unbind();
            Assert.AreSame(_priorTexture, _rawImage.texture);
        }

        [Test]
        public void Unbind_PriorNull_RestoresNull()
        {
            _rawImage.texture = null;
            var sink = new RawImageSink(_rawImage);
            sink.Bind(_rt);
            sink.Unbind();
            Assert.IsNull(_rawImage.texture);
        }

        [Test]
        public void Unbind_WithoutBind_IsNoOp()
        {
            var sink = new RawImageSink(_rawImage);
            Assert.DoesNotThrow(sink.Unbind);
            Assert.AreSame(_priorTexture, _rawImage.texture, "Should not touch target when never bound");
        }

        [Test]
        public void DoubleUnbind_IsNoOp()
        {
            var sink = new RawImageSink(_rawImage);
            sink.Bind(_rt);
            sink.Unbind();
            Assert.DoesNotThrow(sink.Unbind);
            Assert.AreSame(_priorTexture, _rawImage.texture);
        }
    }
}
