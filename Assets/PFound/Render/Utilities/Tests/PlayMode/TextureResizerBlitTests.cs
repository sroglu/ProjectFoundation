using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PFound.Render.Utilities;

namespace PFound.Render.Tests
{
    /// <summary>
    /// PlayMode coverage for the GPU path of <see cref="TextureResizer"/>. These need a
    /// live graphics device: they blit a real source texture into a temporary render
    /// target and read the downscaled pixels back.
    /// </summary>
    public sealed class TextureResizerBlitTests
    {
        private static Texture2D MakeSolid(int width, int height, Color color)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        [UnityTest]
        public IEnumerator Downscale_ProducesOwnedTexture_AtClampedSize()
        {
            var src = MakeSolid(256, 128, Color.red);
            yield return null; // let the upload settle before blitting

            var handle = TextureResizer.Resize(src, 64);
            try
            {
                Assert.IsTrue(handle.OwnsTexture, "A real downscale is owned by the handle.");
                Assert.AreNotSame(src, handle.Texture);
                Assert.AreEqual(64, handle.Texture.width, "Longest axis clamped to the bound.");
                Assert.AreEqual(32, handle.Texture.height, "Aspect ratio preserved.");
            }
            finally
            {
                handle.Dispose();
                Object.Destroy(src);
            }
        }

        [UnityTest]
        public IEnumerator Downscale_PreservesSolidColor()
        {
            var src = MakeSolid(200, 200, new Color(0.2f, 0.6f, 0.9f, 1f));
            yield return null;

            var handle = TextureResizer.Resize(src, 50);
            try
            {
                Color sampled = handle.Texture.GetPixel(handle.Texture.width / 2, handle.Texture.height / 2);
                Assert.That(sampled.r, Is.EqualTo(0.2f).Within(0.05f));
                Assert.That(sampled.g, Is.EqualTo(0.6f).Within(0.05f));
                Assert.That(sampled.b, Is.EqualTo(0.9f).Within(0.05f));
            }
            finally
            {
                handle.Dispose();
                Object.Destroy(src);
            }
        }

        [UnityTest]
        public IEnumerator Downscale_RestoresPreviouslyActiveRenderTarget()
        {
            var src = MakeSolid(300, 100, Color.green);
            yield return null;

            // Set the sentinel AFTER the frame yield — Unity clears RenderTexture.active at
            // frame end, so capturing it before the yield would leave active == null.
            var sentinel = RenderTexture.GetTemporary(16, 16);
            RenderTexture.active = sentinel;
            var handle = TextureResizer.Resize(src, 100);
            try
            {
                Assert.AreSame(sentinel, RenderTexture.active,
                    "The active render target must be restored after the blit.");
            }
            finally
            {
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(sentinel);
                handle.Dispose();
                Object.Destroy(src);
            }
        }

        [UnityTest]
        public IEnumerator Downscale_OwnedTexture_DestroyedOnDispose()
        {
            var src = MakeSolid(128, 128, Color.white);
            yield return null;

            var handle = TextureResizer.Resize(src, 32);
            Texture2D produced = handle.Texture;
            Assert.IsTrue(produced != null);

            handle.Dispose();
            yield return null; // Object.Destroy is deferred to end of frame in play mode

            Assert.IsTrue(produced == null, "Owned downscale destroyed after dispose.");
            Object.Destroy(src);
        }

        [UnityTest]
        public IEnumerator PassThrough_WhenAlreadyWithinBound_DoesNoGpuWork()
        {
            var src = MakeSolid(64, 64, Color.yellow);
            yield return null;

            var handle = TextureResizer.Resize(src, 128);
            try
            {
                Assert.IsFalse(handle.OwnsTexture);
                Assert.AreSame(src, handle.Texture);
            }
            finally
            {
                handle.Dispose(); // no-op
                Object.Destroy(src);
            }
        }
    }
}
