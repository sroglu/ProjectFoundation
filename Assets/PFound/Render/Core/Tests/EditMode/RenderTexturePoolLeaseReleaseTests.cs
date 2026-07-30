using NUnit.Framework;
using UnityEngine.Experimental.Rendering;
using PFound.Render.Core.RenderTextures;

namespace PFound.Render.Core.Tests
{
    public sealed class RenderTexturePoolLeaseReleaseTests
    {
        [Test]
        public void TwoSimultaneousLeases_SameKey_AllocateDistinctRTs()
        {
            var pool = new RenderTexturePool();
            try
            {
                var key = new RenderTextureKey(64, 64, GraphicsFormat.R8G8B8A8_UNorm);
                var a = pool.Lease(key);
                var b = pool.Lease(key);
                Assert.That(a.RT, Is.Not.Null);
                Assert.That(b.RT, Is.Not.Null);
                Assert.That(a.RT, Is.Not.SameAs(b.RT));
                pool.Release(a);
                pool.Release(b);
            }
            finally { pool.Dispose(); }
        }

        [Test]
        public void ReleasedRT_IsReusedOnSubsequentLease()
        {
            var pool = new RenderTexturePool();
            try
            {
                var key = new RenderTextureKey(64, 64, GraphicsFormat.R8G8B8A8_UNorm);
                var a = pool.Lease(key);
                var firstRt = a.RT;
                pool.Release(a);
                var b = pool.Lease(key);
                Assert.That(b.RT, Is.SameAs(firstRt), "Released RT should be reused on subsequent lease with matching key.");
                pool.Release(b);
            }
            finally { pool.Dispose(); }
        }

        [Test]
        public void DifferentKey_AllocatesNewRT()
        {
            var pool = new RenderTexturePool();
            try
            {
                var a = pool.Lease(new RenderTextureKey(64, 64, GraphicsFormat.R8G8B8A8_UNorm));
                var b = pool.Lease(new RenderTextureKey(128, 128, GraphicsFormat.R8G8B8A8_UNorm));
                Assert.That(a.RT, Is.Not.SameAs(b.RT));
                pool.Release(a);
                pool.Release(b);
            }
            finally { pool.Dispose(); }
        }
    }
}
