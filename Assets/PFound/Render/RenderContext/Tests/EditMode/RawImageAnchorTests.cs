using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using PFound.Render.RenderContext;

namespace PFound.Render.Tests
{
    public sealed class RawImageAnchorTests
    {
        private GameObject _canvasGo;
        private Canvas _canvas;
        private RawImage _rawImage;

        [SetUp]
        public void Setup()
        {
            _canvasGo = new GameObject("__canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas = _canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var rawImageGo = new GameObject("__raw", typeof(RectTransform), typeof(RawImage));
            rawImageGo.transform.SetParent(_canvasGo.transform, false);
            _rawImage = rawImageGo.GetComponent<RawImage>();
            _rawImage.rectTransform.sizeDelta = new Vector2(512, 384);
        }

        [TearDown]
        public void Teardown()
        {
            if (_canvasGo != null) UnityEngine.Object.DestroyImmediate(_canvasGo);
        }

        [Test]
        public void Ctor_NullTarget_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new RawImageAnchor(null));
        }

        [Test]
        public void PreferredSize_ReturnsRectSize()
        {
            var anchor = new RawImageAnchor(_rawImage);
            Assert.AreEqual(512, anchor.PreferredWidth);
            Assert.AreEqual(384, anchor.PreferredHeight);
        }

        [Test]
        public void TargetAlive_TrueWhenAttached()
        {
            var anchor = new RawImageAnchor(_rawImage);
            Assert.IsTrue(anchor.TargetAlive);
        }

        [Test]
        public void TargetAlive_FalseAfterDestroy()
        {
            var anchor = new RawImageAnchor(_rawImage);
            UnityEngine.Object.DestroyImmediate(_rawImage);
            Assert.IsFalse(anchor.TargetAlive);
        }

        [Test]
        public void Target_ReturnsRawImageReference()
        {
            var anchor = new RawImageAnchor(_rawImage);
            Assert.AreSame(_rawImage, anchor.Target);
        }

        [Test]
        public void CreateSink_ReturnsRawImageSink()
        {
            var anchor = new RawImageAnchor(_rawImage);
            var sink = anchor.CreateSink();
            Assert.IsInstanceOf<RawImageSink>(sink);
        }
    }
}
