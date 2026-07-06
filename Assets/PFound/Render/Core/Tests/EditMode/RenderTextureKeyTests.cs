using NUnit.Framework;
using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using PFound.Render.Core.RenderTextures;

namespace PFound.Render.Core.Tests
{
    public sealed class RenderTextureKeyTests
    {
        [Test]
        public void Equality_StructuralAcrossAllFields()
        {
            var a = new RenderTextureKey(640, 480, GraphicsFormat.R8G8B8A8_UNorm, 24, 1, false);
            var b = new RenderTextureKey(640, 480, GraphicsFormat.R8G8B8A8_UNorm, 24, 1, false);
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void Inequality_OnAnyFieldChange()
        {
            var baseKey = new RenderTextureKey(640, 480, GraphicsFormat.R8G8B8A8_UNorm, 24, 1, false);
            Assert.That(new RenderTextureKey(641, 480, GraphicsFormat.R8G8B8A8_UNorm, 24, 1, false), Is.Not.EqualTo(baseKey));
            Assert.That(new RenderTextureKey(640, 481, GraphicsFormat.R8G8B8A8_UNorm, 24, 1, false), Is.Not.EqualTo(baseKey));
            Assert.That(new RenderTextureKey(640, 480, GraphicsFormat.R16G16B16A16_SFloat, 24, 1, false), Is.Not.EqualTo(baseKey));
            Assert.That(new RenderTextureKey(640, 480, GraphicsFormat.R8G8B8A8_UNorm, 16, 1, false), Is.Not.EqualTo(baseKey));
            Assert.That(new RenderTextureKey(640, 480, GraphicsFormat.R8G8B8A8_UNorm, 24, 4, false), Is.Not.EqualTo(baseKey));
            Assert.That(new RenderTextureKey(640, 480, GraphicsFormat.R8G8B8A8_UNorm, 24, 1, true ), Is.Not.EqualTo(baseKey));
        }

        [Test]
        public void Constructor_RejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RenderTextureKey(0, 480, GraphicsFormat.R8G8B8A8_UNorm));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RenderTextureKey(640, 0, GraphicsFormat.R8G8B8A8_UNorm));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RenderTextureKey(640, 480, GraphicsFormat.R8G8B8A8_UNorm, depthBits: 7));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RenderTextureKey(640, 480, GraphicsFormat.R8G8B8A8_UNorm, msaa: 3));
        }

        [Test]
        public void ToString_IsHumanReadable()
        {
            var k = new RenderTextureKey(1920, 1080, GraphicsFormat.R8G8B8A8_UNorm, 24, 1, false);
            var s = k.ToString();
            Assert.That(s, Does.Contain("1920x1080"));
            Assert.That(s, Does.Contain("depth=24"));
            Assert.That(s, Does.Contain("MSAA=1"));
        }

        [Test]
        public void ColorSpaceOverload_FoldsLinearAndSrgbToDistinctKeys()
        {
            var linear = new RenderTextureKey(256, 256, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var srgb = new RenderTextureKey(256, 256, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Assert.That(linear, Is.Not.EqualTo(srgb), "Linear and sRGB must fold to distinct GraphicsFormat values");
            Assert.That(linear.Format, Is.Not.EqualTo(srgb.Format));
        }

        [Test]
        public void ColorSpaceOverload_DerivesHdrFromResolvedFormat()
        {
            var ldr = new RenderTextureKey(256, 256, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var hdr = new RenderTextureKey(256, 256, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            Assert.That(ldr.HDR, Is.False, "ARGB32 should not be HDR");
            Assert.That(hdr.HDR, Is.True, "ARGBHalf should be HDR");
        }

        [Test]
        public void ColorSpaceOverload_MatchesExplicitGraphicsFormatPath()
        {
            var viaOverload = new RenderTextureKey(640, 480, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 24, 1);
            // GetGraphicsFormat(format, RenderTextureReadWrite) collapses sRGB to the UNorm base, so
            // the explicit path must select the sRGB sibling to match the overload's color-space fold.
            var explicitGF = GraphicsFormatUtility.GetSRGBFormat(
                GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear));
            var viaExplicit = new RenderTextureKey(640, 480, explicitGF, 24, 1, GraphicsFormatUtility.IsHDRFormat(explicitGF));
            Assert.That(viaOverload, Is.EqualTo(viaExplicit));
            Assert.That(viaOverload.GetHashCode(), Is.EqualTo(viaExplicit.GetHashCode()));
        }
    }
}
