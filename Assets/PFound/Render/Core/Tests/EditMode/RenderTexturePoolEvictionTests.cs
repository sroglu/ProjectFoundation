using NUnit.Framework;
using UnityEngine.Experimental.Rendering;
using PFound.Render.Core.RenderTextures;

namespace PFound.Render.Core.Tests
{
    public sealed class RenderTexturePoolEvictionTests
    {
        [Test]
        public void IdleEntry_PastThreshold_IsEvicted()
        {
            var options = new RenderTexturePoolOptions(idleFrameThreshold: 5);
            var pool = new RenderTexturePool(options);
            try
            {
                var key = new RenderTextureKey(32, 32, GraphicsFormat.R8G8B8A8_UNorm);
                var lease = pool.Lease(key);
                int releasedFrame = UnityEngine.Time.frameCount;
                pool.Release(lease);

                // Advance Tick past idle threshold.
                pool.Tick(releasedFrame + 4);
                // Still alive (< threshold).
                var stillThere = pool.Lease(key);
                Assert.That(stillThere.RT, Is.SameAs(lease.RT), "Within threshold the RT must be reused.");
                pool.Release(stillThere);

                pool.Tick(releasedFrame + 100); // Far past threshold => evicted.
                var fresh = pool.Lease(key);
                Assert.That(fresh.RT, Is.Not.SameAs(lease.RT), "Past threshold a fresh RT must be allocated.");
                pool.Release(fresh);
            }
            finally { pool.Dispose(); }
        }
    }
}
