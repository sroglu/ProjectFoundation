using NUnit.Framework;
using UnityEngine;
using PFound.Render.RenderContext;

namespace PFound.Render.Tests
{
    public sealed class RenderContextPoolKeyTests
    {
        [Test]
        public void Equality_SameSixFields_AreEqual()
        {
            var d = RenderContextDescriptor.Default;
            var a = RenderContextPoolKey.FromDescriptor(in d, 512, 256);
            var b = RenderContextPoolKey.FromDescriptor(in d, 512, 256);
            Assert.IsTrue(a.Equals(b));
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void Equality_DifferentWidth_AreNotEqual()
        {
            var d = RenderContextDescriptor.Default;
            var a = RenderContextPoolKey.FromDescriptor(in d, 512, 256);
            var b = RenderContextPoolKey.FromDescriptor(in d, 513, 256);
            Assert.IsFalse(a.Equals(b));
        }

        [Test]
        public void Equality_DifferentFormat_AreNotEqual()
        {
            var d1 = RenderContextDescriptor.Default;
            var d2 = RenderContextDescriptor.Default;
            d2.Format = RenderTextureFormat.ARGBHalf;
            var a = RenderContextPoolKey.FromDescriptor(in d1, 512, 256);
            var b = RenderContextPoolKey.FromDescriptor(in d2, 512, 256);
            Assert.IsFalse(a.Equals(b));
        }

        [Test]
        public void Key_IgnoresCullingMaskClearFlagsFieldOfView()
        {
            // The pool key is six fields; changes to per-lease camera settings must NOT change the key.
            var d1 = RenderContextDescriptor.Default;
            d1.CullingMask = 1 << 5;
            d1.ClearFlags = CameraClearFlags.Skybox;
            d1.BackgroundColor = Color.red;
            d1.FieldOfView = 90f;

            var d2 = RenderContextDescriptor.Default;
            d2.CullingMask = ~0;
            d2.ClearFlags = CameraClearFlags.Color;
            d2.BackgroundColor = Color.blue;
            d2.FieldOfView = 45f;

            var a = RenderContextPoolKey.FromDescriptor(in d1, 512, 256);
            var b = RenderContextPoolKey.FromDescriptor(in d2, 512, 256);
            Assert.IsTrue(a.Equals(b));
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void Key_IncludesDepthBitsMsaaColorSpace()
        {
            var d = RenderContextDescriptor.Default;
            var baseline = RenderContextPoolKey.FromDescriptor(in d, 256, 256);

            d.DepthBits = 24;
            Assert.IsFalse(baseline.Equals(RenderContextPoolKey.FromDescriptor(in d, 256, 256)),
                "DepthBits must distinguish keys");

            d = RenderContextDescriptor.Default;
            d.Msaa = 2;
            Assert.IsFalse(baseline.Equals(RenderContextPoolKey.FromDescriptor(in d, 256, 256)),
                "Msaa must distinguish keys");

            d = RenderContextDescriptor.Default;
            d.ColorSpace = RenderTextureReadWrite.Linear;
            Assert.IsFalse(baseline.Equals(RenderContextPoolKey.FromDescriptor(in d, 256, 256)),
                "ColorSpace must distinguish keys");
        }
    }
}
