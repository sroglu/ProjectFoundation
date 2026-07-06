using System;
using NUnit.Framework;
using UnityEngine;
using PFound.Render.RenderContext;

namespace PFound.Render.Tests
{
    public sealed class RenderContextDescriptorTests
    {
        [Test]
        public void Default_HasSensibleValues()
        {
            var d = RenderContextDescriptor.Default;
            Assert.AreEqual(0, d.Width);
            Assert.AreEqual(0, d.Height);
            Assert.AreEqual(RenderTextureFormat.ARGB32, d.Format);
            Assert.AreEqual(16, d.DepthBits);
            Assert.AreEqual(1, d.Msaa);
            Assert.AreEqual(RenderTextureReadWrite.Default, d.ColorSpace);
            Assert.AreEqual(CameraClearFlags.SolidColor, d.ClearFlags);
            Assert.IsFalse(d.Orthographic);
            Assert.AreEqual(5f, d.OrthographicSize);
            Assert.AreEqual(60f, d.FieldOfView);
        }

        [Test]
        public void Validate_AcceptsDefault()
        {
            var d = RenderContextDescriptor.Default;
            Assert.DoesNotThrow(() => d.Validate());
        }

        [TestCase(1)]
        [TestCase(8)]
        [TestCase(32)]
        [TestCase(-1)]
        public void Validate_RejectsInvalidDepthBits(int depth)
        {
            var d = RenderContextDescriptor.Default;
            d.DepthBits = depth;
            var ex = Assert.Throws<ArgumentException>(() => d.Validate());
            StringAssert.Contains(nameof(d.DepthBits), ex.ParamName);
        }

        [TestCase(0)]
        [TestCase(3)]
        [TestCase(5)]
        [TestCase(16)]
        public void Validate_RejectsInvalidMsaa(int msaa)
        {
            var d = RenderContextDescriptor.Default;
            d.Msaa = msaa;
            var ex = Assert.Throws<ArgumentException>(() => d.Validate());
            StringAssert.Contains(nameof(d.Msaa), ex.ParamName);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void Validate_RejectsNonPositiveOrthographicSize_WhenOrthographic(float size)
        {
            var d = RenderContextDescriptor.Default;
            d.Orthographic = true;
            d.OrthographicSize = size;
            Assert.Throws<ArgumentException>(() => d.Validate());
        }

        [TestCase(0f)]
        [TestCase(180f)]
        [TestCase(-30f)]
        [TestCase(200f)]
        public void Validate_RejectsFovOutOfRange_WhenPerspective(float fov)
        {
            var d = RenderContextDescriptor.Default;
            d.Orthographic = false;
            d.FieldOfView = fov;
            Assert.Throws<ArgumentException>(() => d.Validate());
        }

        [TestCase(-1)]
        [TestCase(-100)]
        public void Validate_RejectsNegativeWidth(int width)
        {
            var d = RenderContextDescriptor.Default;
            d.Width = width;
            Assert.Throws<ArgumentException>(() => d.Validate());
        }

        [TestCase(-1)]
        public void Validate_RejectsNegativeHeight(int height)
        {
            var d = RenderContextDescriptor.Default;
            d.Height = height;
            Assert.Throws<ArgumentException>(() => d.Validate());
        }

        [Test]
        public void Equality_TwoDefaultsAreEqual()
        {
            var a = RenderContextDescriptor.Default;
            var b = RenderContextDescriptor.Default;
            Assert.IsTrue(a == b);
            Assert.IsTrue(a.Equals(b));
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }
    }
}
