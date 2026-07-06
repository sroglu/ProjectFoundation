using NUnit.Framework;
using UnityEngine.Experimental.Rendering;
using PFound.Render.Core.RenderTextures;

namespace PFound.Render.Core.Tests
{
    public sealed class RenderTexturePoolLeakDetectionTests
    {
        [Test]
        public void OutstandingLease_PastLeakThreshold_WritesRingBufferEntry()
        {
            var options = new RenderTexturePoolOptions(leakFrameThreshold: 5);
            var pool = new RenderTexturePool(options) { LogLeaksToConsole = false };
            try
            {
                var key = new RenderTextureKey(64, 64, GraphicsFormat.R8G8B8A8_UNorm);
                var lease = pool.Lease(key);
                int frame = UnityEngine.Time.frameCount;

                pool.Tick(frame + 10); // past threshold

                bool got = pool.TryReadLeak(out var entry);
                Assert.That(got, Is.True, "A leak entry should be in the ring buffer.");
                Assert.That(entry.LeasedFrame, Is.EqualTo(frame));
                Assert.That(entry.ReportedFrame, Is.EqualTo(frame + 10));

                pool.Release(lease);
            }
            finally { pool.Dispose(); }
        }

        [Test]
        public void SameLease_NotReportedTwice()
        {
            var options = new RenderTexturePoolOptions(leakFrameThreshold: 5);
            var pool = new RenderTexturePool(options) { LogLeaksToConsole = false };
            try
            {
                var key = new RenderTextureKey(64, 64, GraphicsFormat.R8G8B8A8_UNorm);
                var lease = pool.Lease(key);
                int frame = UnityEngine.Time.frameCount;
                pool.Tick(frame + 10);
                pool.Tick(frame + 20);

                int count = 0;
                while (pool.TryReadLeak(out _)) count++;
                Assert.That(count, Is.EqualTo(1), "Same outstanding lease must not be reported twice.");

                pool.Release(lease);
            }
            finally { pool.Dispose(); }
        }
    }
}
