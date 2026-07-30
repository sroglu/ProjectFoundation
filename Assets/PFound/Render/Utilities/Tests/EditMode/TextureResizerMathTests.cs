using NUnit.Framework;
using UnityEngine;
using PFound.Render.Utilities;

namespace PFound.Render.Tests
{
    /// <summary>
    /// EditMode coverage for <see cref="TextureResizer"/> — the size math and the
    /// <see cref="TextureResizeHandle"/> ownership semantics. The GPU blit path itself
    /// is exercised by the PlayMode suite (it needs a live graphics device).
    /// </summary>
    public sealed class TextureResizerMathTests
    {
        // ----- size math -----------------------------------------------------

        [Test]
        public void WithinBound_ReportsNoResize_AndEchoesSource()
        {
            bool needs = TextureResizer.TryGetDownscaledSize(64, 32, 128, out int w, out int h);

            Assert.IsFalse(needs, "Source already fits the bound.");
            Assert.AreEqual(64, w);
            Assert.AreEqual(32, h);
        }

        [Test]
        public void ExactlyOnBound_ReportsNoResize()
        {
            bool needs = TextureResizer.TryGetDownscaledSize(128, 128, 128, out _, out _);
            Assert.IsFalse(needs);
        }

        [Test]
        public void Landscape_ClampsWidthToMax_PreservesAspect()
        {
            bool needs = TextureResizer.TryGetDownscaledSize(400, 200, 100, out int w, out int h);

            Assert.IsTrue(needs);
            Assert.AreEqual(100, w, "Longest axis maps onto the bound exactly.");
            Assert.AreEqual(50, h, "Shorter axis scaled by the same ratio.");
        }

        [Test]
        public void Portrait_ClampsHeightToMax_PreservesAspect()
        {
            bool needs = TextureResizer.TryGetDownscaledSize(200, 400, 100, out int w, out int h);

            Assert.IsTrue(needs);
            Assert.AreEqual(100, h);
            Assert.AreEqual(50, w);
        }

        [Test]
        public void Square_ClampsBothToMax()
        {
            TextureResizer.TryGetDownscaledSize(512, 512, 256, out int w, out int h);
            Assert.AreEqual(256, w);
            Assert.AreEqual(256, h);
        }

        [Test]
        public void ExtremeAspect_ShortAxisNeverCollapsesBelowOne()
        {
            // 1000x1 shrunk to a bound of 10 would round the height to 0 without the guard.
            bool needs = TextureResizer.TryGetDownscaledSize(1000, 1, 10, out int w, out int h);

            Assert.IsTrue(needs);
            Assert.AreEqual(10, w);
            Assert.AreEqual(1, h, "Min-1 guard keeps the texture at least a pixel tall.");
        }

        [Test]
        public void AspectRatioPreservedWithinOnePixel()
        {
            const int srcW = 1920;
            const int srcH = 1080;
            TextureResizer.TryGetDownscaledSize(srcW, srcH, 640, out int w, out int h);

            Assert.AreEqual(640, w);
            float expectedH = srcH * (640f / srcW);
            Assert.That(h, Is.EqualTo(Mathf.RoundToInt(expectedH)).Within(1),
                "Height stays within a pixel of the ideal aspect projection.");
        }

        // ----- handle semantics ---------------------------------------------

        [Test]
        public void NullSource_ReturnsNonOwningPassThrough()
        {
            using var handle = TextureResizer.Resize(null, 128);
            Assert.IsFalse(handle.OwnsTexture);
            Assert.IsNull(handle.Texture);
        }

        [Test]
        public void NonPositiveMax_ReturnsNonOwningPassThrough()
        {
            var src = new Texture2D(64, 64);
            try
            {
                using var handle = TextureResizer.Resize(src, 0);
                Assert.IsFalse(handle.OwnsTexture);
                Assert.AreSame(src, handle.Texture, "Original instance handed straight back.");
            }
            finally
            {
                Object.DestroyImmediate(src);
            }
        }

        [Test]
        public void WithinBound_ReturnsSameInstance_NonOwning()
        {
            var src = new Texture2D(64, 64);
            try
            {
                using var handle = TextureResizer.Resize(src, 128);
                Assert.IsFalse(handle.OwnsTexture);
                Assert.AreSame(src, handle.Texture);
            }
            finally
            {
                Object.DestroyImmediate(src);
            }
        }

        [Test]
        public void Dispose_NonOwning_LeavesTextureAlive()
        {
            var src = new Texture2D(32, 32);
            try
            {
                var handle = new TextureResizeHandle(src, false);
                handle.Dispose();
                Assert.IsTrue(src != null, "Pass-through dispose must not destroy the source.");
            }
            finally
            {
                Object.DestroyImmediate(src);
            }
        }

        [Test]
        public void Dispose_Owning_DestroysTexture_AndIsIdempotent()
        {
            var owned = new Texture2D(16, 16);
            var handle = new TextureResizeHandle(owned, true);

            handle.Dispose();
            Assert.IsTrue(owned == null, "Owned texture destroyed on dispose.");

            Assert.DoesNotThrow(() => handle.Dispose(), "Second dispose is a safe no-op.");
        }
    }
}
