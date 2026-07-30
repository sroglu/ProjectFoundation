using NUnit.Framework;
using UnityEngine;
using PFound.Render.Utilities;

namespace PFound.Render.Tests
{
    // GPU-dependent behaviours (Graphics.Blit / ReadPixels / live RenderTextures)
    // run in Play Mode where a graphics device is available.
    public sealed class ReadableCopyTests
    {
        [Test]
        public void CreateReadableCopy_PreservesSizeAndContent()
        {
            var source = TextureFactory.CreateSolidTexture(4, 4, Color.green);
            var copy = TextureFactory.CreateReadableCopy(source);
            try
            {
                Assert.AreEqual(4, copy.width);
                Assert.AreEqual(4, copy.height);

                var pixel = copy.GetPixel(1, 1);
                Assert.AreEqual(0f, pixel.r, 0.02f);
                Assert.AreEqual(1f, pixel.g, 0.02f);
                Assert.AreEqual(0f, pixel.b, 0.02f);
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(copy);
            }
        }

        [Test]
        public void CreateReadableCopy_RescalesToTargetSize()
        {
            var source = TextureFactory.CreateSolidTexture(8, 8, Color.blue);
            var copy = TextureFactory.CreateReadableCopy(source, 2, 3);
            try
            {
                Assert.AreEqual(2, copy.width);
                Assert.AreEqual(3, copy.height);
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(copy);
            }
        }

        [Test]
        public void AutoSizedRenderTexture_ReusesTextureWhenSizeUnchanged()
        {
            using (var pool = new AutoSizedRenderTexture())
            {
                var first = pool.GetForSize(64, 64);
                var again = pool.GetForSize(64, 64);
                Assert.AreSame(first, again);
                Assert.AreEqual(64, first.width);
                Assert.AreEqual(64, first.height);
            }
        }

        [Test]
        public void AutoSizedRenderTexture_ReallocatesOnSizeChange()
        {
            using (var pool = new AutoSizedRenderTexture())
            {
                var small = pool.GetForSize(32, 32);
                var large = pool.GetForSize(128, 96);
                Assert.AreEqual(128, large.width);
                Assert.AreEqual(96, large.height);
                Assert.AreNotSame(small, large);
            }
        }

        [Test]
        public void AutoSizedRenderTexture_DisposeReleasesTexture()
        {
            var pool = new AutoSizedRenderTexture();
            pool.GetForSize(16, 16);
            pool.Dispose();
            Assert.IsNull(pool.Current);
        }
    }
}
