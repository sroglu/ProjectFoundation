using NUnit.Framework;
using UnityEngine;
using PFound.Utilities.ColorTools;

namespace PFound.Utilities.ColorTools.Tests
{
    /// <summary>
    /// EditMode tests for the ColorTools extension methods. These require UnityEngine
    /// (<see cref="Color"/> / <see cref="Color32"/>) and therefore run under the Unity Test
    /// Runner rather than the standalone mono runner used by engine-free modules.
    /// </summary>
    public sealed class ColorToolsTests
    {
        [Test]
        public void ExactEquality_ComparesChannels()
        {
            var a = new Color(0.25f, 0.5f, 0.75f, 1f);
            Assert.IsTrue(a.EqualsRgba(new Color(0.25f, 0.5f, 0.75f, 1f)));
            Assert.IsFalse(a.EqualsRgba(new Color(0.25f, 0.5f, 0.75f, 0.5f)));
            Assert.IsTrue(a.EqualsRgb(new Color(0.25f, 0.5f, 0.75f, 0.5f)));
            Assert.IsFalse(a.EqualsRgb(new Color(0.25f, 0.5f, 0.76f, 1f)));
        }

        [Test]
        public void ApproximateEquality_HonoursTolerance()
        {
            var a = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            var b = new Color(0.5005f, 0.4995f, 0.5f, 0.5f);
            Assert.IsTrue(a.ApproximatelyEqualsRgba(b, 0.01f));
            Assert.IsFalse(a.ApproximatelyEqualsRgba(b, 0.0001f));

            var c = new Color(0.5f, 0.5f, 0.5f, 0.9f);
            Assert.IsTrue(a.ApproximatelyEqualsRgb(c, 0.0001f));
            Assert.IsFalse(a.ApproximatelyEqualsAlpha(c, 0.01f));
            Assert.IsTrue(a.ApproximatelyEqualsAlpha(new Color(1f, 1f, 1f, 0.5005f), 0.01f));
        }

        [Test]
        public void PackedInt32_RoundTrips()
        {
            var color = new Color32(18, 52, 86, 120);
            int packed = color.ToInt32();
            Assert.AreEqual(unchecked((int)0x12345678), packed);

            Color32 restored = ColorTools.ToColor32(packed);
            Assert.AreEqual(color.r, restored.r);
            Assert.AreEqual(color.g, restored.g);
            Assert.AreEqual(color.b, restored.b);
            Assert.AreEqual(color.a, restored.a);
        }

        [Test]
        public void ColorAndColor32_Convert()
        {
            var color = new Color(1f, 0f, 0f, 1f);
            Color32 c32 = color.ToColor32();
            Assert.AreEqual(255, c32.r);
            Assert.AreEqual(0, c32.g);

            Color back = c32.ToColor();
            Assert.That(back.r, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void FastLerp_IsUnclampedAndLinear()
        {
            var a = new Color(0f, 0f, 0f, 0f);
            var b = new Color(1f, 1f, 1f, 1f);
            Color mid = a.FastLerp(b, 0.5f);
            Assert.That(mid.r, Is.EqualTo(0.5f).Within(0.0001f));

            Color beyond = a.FastLerp(b, 2f);
            Assert.That(beyond.r, Is.EqualTo(2f).Within(0.0001f)); // not clamped

            Color32 mid32 = new Color32(0, 0, 0, 0).FastLerp(new Color32(100, 100, 100, 100), 0.5f);
            Assert.AreEqual(50, mid32.r);
        }

        [Test]
        public void Hsl_RoundTrips()
        {
            Color[] samples =
            {
                new Color(0.8f, 0.2f, 0.3f, 1f),
                new Color(0.1f, 0.6f, 0.9f, 1f),
                new Color(0.5f, 0.5f, 0.5f, 1f), // achromatic
                Color.white,
                Color.black,
            };

            foreach (Color original in samples)
            {
                original.ToHsl(out float h, out float s, out float l);
                Color rebuilt = ColorTools.FromHsl(h, s, l, original.a);
                Assert.That(rebuilt.r, Is.EqualTo(original.r).Within(0.001f), "R for " + original);
                Assert.That(rebuilt.g, Is.EqualTo(original.g).Within(0.001f), "G for " + original);
                Assert.That(rebuilt.b, Is.EqualTo(original.b).Within(0.001f), "B for " + original);
            }
        }

        [Test]
        public void AdjustBrightness_ScalesAndClamps()
        {
            var color = new Color(0.4f, 0.5f, 0.6f, 0.7f);
            Color darker = color.AdjustBrightness(0.5f);
            Assert.That(darker.r, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(darker.a, Is.EqualTo(0.7f).Within(0.0001f)); // alpha preserved

            Color clamped = color.AdjustBrightness(10f);
            Assert.That(clamped.b, Is.EqualTo(1f).Within(0.0001f)); // clamped to 1
        }

        [Test]
        public void Hex_FormatsAndParses()
        {
            var color = new Color32(255, 128, 0, 64);
            Assert.AreEqual("#FF8000", color.ToHexString());
            Assert.AreEqual("#FF800040", color.ToHexString(true));

            Assert.IsTrue(ColorTools.TryParseHex("#FF8000", out Color rgb));
            Assert.That(rgb.r, Is.EqualTo(1f).Within(0.001f));
            Assert.That(rgb.a, Is.EqualTo(1f).Within(0.001f)); // default alpha

            Assert.IsTrue(ColorTools.TryParseHex("00FF00FF", out Color noHash)); // '#' optional
            Assert.That(noHash.g, Is.EqualTo(1f).Within(0.001f));

            Assert.IsFalse(ColorTools.TryParseHex("#FFF", out _));    // wrong length
            Assert.IsFalse(ColorTools.TryParseHex("#GGGGGG", out _)); // non-hex
            Assert.IsFalse(ColorTools.TryParseHex(null, out _));
        }

        [Test]
        public void ParseHex_ThrowsOnInvalid()
        {
            Assert.Throws<System.FormatException>(() => ColorTools.ParseHex("nope"));
        }

        [Test]
        public void WithAlpha_ReplacesAlphaOnly()
        {
            var color = new Color(0.1f, 0.2f, 0.3f, 1f);
            Color faded = color.WithAlpha(0.25f);
            Assert.That(faded.a, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(faded.r, Is.EqualTo(0.1f).Within(0.0001f));

            Color32 faded32 = new Color32(10, 20, 30, 255).WithAlpha(128);
            Assert.AreEqual(128, faded32.a);
            Assert.AreEqual(10, faded32.r);
        }
    }
}
