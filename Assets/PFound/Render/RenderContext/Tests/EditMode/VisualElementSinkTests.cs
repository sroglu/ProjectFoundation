using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using PFound.Render.RenderContext;

namespace PFound.Render.Tests
{
    public sealed class VisualElementSinkTests
    {
        private VisualElement _ve;
        private RenderTexture _rt;

        [SetUp]
        public void Setup()
        {
            _ve = new VisualElement();
            _rt = new RenderTexture(64, 64, 0);
            _rt.Create();
        }

        [TearDown]
        public void Teardown()
        {
            if (_rt != null) { _rt.Release(); UnityEngine.Object.DestroyImmediate(_rt); }
        }

        [Test]
        public void Ctor_NullTarget_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new VisualElementSink(null));
        }

        [Test]
        public void Bind_AttachesImageChildWithRt()
        {
            var sink = new VisualElementSink(_ve);
            sink.Bind(_rt);
            var img = _ve.Q<Image>(VisualElementSink.ChildName);
            Assert.IsNotNull(img, "Sink should attach an Image child");
            Assert.AreSame(_rt, img.image, "Image child must reference the supplied RT");
        }

        [Test]
        public void Unbind_RemovesImageChild()
        {
            var sink = new VisualElementSink(_ve);
            sink.Bind(_rt);
            sink.Unbind();
            var img = _ve.Q<Image>(VisualElementSink.ChildName);
            Assert.IsNull(img, "Sink should remove the Image child on Unbind");
        }

        [Test]
        public void Unbind_WithoutBind_IsNoOp()
        {
            var sink = new VisualElementSink(_ve);
            Assert.DoesNotThrow(sink.Unbind);
            Assert.AreEqual(0, _ve.childCount);
        }

        [Test]
        public void DoubleUnbind_IsNoOp()
        {
            var sink = new VisualElementSink(_ve);
            sink.Bind(_rt);
            sink.Unbind();
            Assert.DoesNotThrow(sink.Unbind);
        }

        [Test]
        public void Bind_TwiceWithDifferentRt_SwapsRt()
        {
            var sink = new VisualElementSink(_ve);
            sink.Bind(_rt);
            var rt2 = new RenderTexture(128, 128, 0);
            rt2.Create();
            try
            {
                sink.Bind(rt2);
                var img = _ve.Q<Image>(VisualElementSink.ChildName);
                Assert.IsNotNull(img);
                Assert.AreSame(rt2, img.image, "Bind on already-bound sink must swap RT in place");
            }
            finally
            {
                rt2.Release();
                UnityEngine.Object.DestroyImmediate(rt2);
            }
        }
    }
}
