using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using PFound.Render.RenderContext;

namespace PFound.Render.Tests
{
    public sealed class VisualElementAnchorTests
    {
        [Test]
        public void Ctor_NullTarget_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new VisualElementAnchor(null));
        }

        [Test]
        public void PreferredSize_BeforeLayout_ReturnsZero()
        {
            var ve = new VisualElement();
            var anchor = new VisualElementAnchor(ve);
            // resolvedStyle.width/height is NaN until laid out via a panel.
            Assert.AreEqual(0, anchor.PreferredWidth);
            Assert.AreEqual(0, anchor.PreferredHeight);
        }

        [Test]
        public void TargetAlive_DetachedElement_ReturnsFalse()
        {
            var ve = new VisualElement();
            var anchor = new VisualElementAnchor(ve);
            // No panel assigned → not in visual tree → not alive (per contract FR-014).
            Assert.IsFalse(anchor.TargetAlive);
        }

        [Test]
        public void Target_ReturnsVisualElement()
        {
            var ve = new VisualElement();
            var anchor = new VisualElementAnchor(ve);
            Assert.AreSame(ve, anchor.Target);
        }

        [Test]
        public void CreateSink_ReturnsVisualElementSink()
        {
            var ve = new VisualElement();
            var anchor = new VisualElementAnchor(ve);
            var sink = anchor.CreateSink();
            Assert.IsInstanceOf<VisualElementSink>(sink);
        }

        [Test]
        public void PreferredSize_DisplayNone_ReturnsZero()
        {
            var ve = new VisualElement();
            ve.style.display = DisplayStyle.None;
            var anchor = new VisualElementAnchor(ve);
            Assert.AreEqual(0, anchor.PreferredWidth);
            Assert.AreEqual(0, anchor.PreferredHeight);
        }
    }
}
