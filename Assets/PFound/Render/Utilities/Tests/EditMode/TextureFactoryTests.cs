using NUnit.Framework;
using UnityEngine;
using PFound.Render.Utilities;

namespace PFound.Render.Utilities.Tests
{
    public sealed class TextureFactoryTests
    {
        const float Tolerance = 0.001f;

        static void AssertColor(Color expected, Color actual)
        {
            Assert.AreEqual(expected.r, actual.r, Tolerance, "r");
            Assert.AreEqual(expected.g, actual.g, Tolerance, "g");
            Assert.AreEqual(expected.b, actual.b, Tolerance, "b");
            Assert.AreEqual(expected.a, actual.a, Tolerance, "a");
        }

        [Test]
        public void SolidTexture_HasRequestedSize()
        {
            var tex = TextureFactory.CreateSolidTexture(8, 4, Color.red);
            try
            {
                Assert.AreEqual(8, tex.width);
                Assert.AreEqual(4, tex.height);
            }
            finally { Object.DestroyImmediate(tex); }
        }

        [Test]
        public void SolidTexture_EveryPixelMatchesFill()
        {
            var fill = new Color(0.2f, 0.4f, 0.6f, 0.8f);
            var tex = TextureFactory.CreateSolidTexture(4, 4, fill);
            try
            {
                foreach (var pixel in tex.GetPixels())
                    AssertColor(fill, pixel);
            }
            finally { Object.DestroyImmediate(tex); }
        }

        [Test]
        public void VerticalGradient_RunsBottomToTop()
        {
            var tex = TextureFactory.CreateVerticalGradient(2, 3, Color.black, Color.white);
            try
            {
                // Row 0 is the "from" end, top row is the "to" end.
                AssertColor(Color.black, tex.GetPixel(0, 0));
                AssertColor(Color.white, tex.GetPixel(0, 2));
                // Columns share the same value on a given row.
                AssertColor(tex.GetPixel(0, 1), tex.GetPixel(1, 1));
            }
            finally { Object.DestroyImmediate(tex); }
        }

        [Test]
        public void HorizontalGradient_RunsLeftToRight()
        {
            var tex = TextureFactory.CreateHorizontalGradient(3, 2, Color.black, Color.white);
            try
            {
                AssertColor(Color.black, tex.GetPixel(0, 0));
                AssertColor(Color.white, tex.GetPixel(2, 0));
                // Rows share the same value on a given column.
                AssertColor(tex.GetPixel(1, 0), tex.GetPixel(1, 1));
            }
            finally { Object.DestroyImmediate(tex); }
        }

        [Test]
        public void TintTexture_MultipliesPixels()
        {
            var source = TextureFactory.CreateSolidTexture(2, 2, new Color(0.8f, 0.8f, 0.8f, 1f));
            var tint = new Color(0.5f, 1f, 0.25f, 1f);
            var tinted = TextureFactory.TintTexture(source, tint);
            try
            {
                var expected = new Color(0.4f, 0.8f, 0.2f, 1f);
                foreach (var pixel in tinted.GetPixels())
                    AssertColor(expected, pixel);
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(tinted);
            }
        }
    }
}
