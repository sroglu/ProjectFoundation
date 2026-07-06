using NUnit.Framework;
using UnityEngine.Experimental.Rendering;
using PFound.Render.Core.RenderTextures;

namespace PFound.Render.Core.Tests
{
    public sealed class RenderTexturePoolOverflowTests
    {
        [Test]
        public void MoreLeaksThanCapacity_DropsOldest_IncrementsDroppedCount()
        {
            const int capacity = 4;
            var options = new RenderTexturePoolOptions(
                leakFrameThreshold: 1,
                leakRingBufferCapacity: capacity);
            var pool = new RenderTexturePool(options) { LogLeaksToConsole = false };
            try
            {
                int frame = UnityEngine.Time.frameCount;
                // Lease 6 distinct keys, then sweep — produces 6 leaks into a capacity=4 buffer.
                for (int i = 0; i < 6; i++)
                {
                    pool.Lease(new RenderTextureKey(64 + i, 64, GraphicsFormat.R8G8B8A8_UNorm));
                }
                pool.Tick(frame + 5);

                Assert.That(pool.DroppedLeakCount, Is.EqualTo(2L), "Excess leaks beyond capacity must be counted as dropped.");
                int drained = 0;
                while (pool.TryReadLeak(out _)) drained++;
                Assert.That(drained, Is.EqualTo(capacity), "Ring buffer must hold exactly capacity entries.");
            }
            finally { pool.Dispose(); }
        }
    }
}
