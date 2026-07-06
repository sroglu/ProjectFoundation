using NUnit.Framework;
using UnityEngine.Experimental.Rendering;
using PFound.Render.Core.RenderTextures;

namespace PFound.Render.Core.Tests
{
    public sealed class RenderTextureLeaseTests
    {
        [Test]
        public void DefaultLease_DisposeIsNoOp()
        {
            var defaultLease = default(RenderTextureLease);
            Assert.DoesNotThrow(() => defaultLease.Dispose());
        }

        [Test]
        public void Lease_DoubleDispose_IsSafe()
        {
            var pool = new RenderTexturePool();
            try
            {
                var lease = pool.Lease(new RenderTextureKey(64, 64, GraphicsFormat.R8G8B8A8_UNorm));
                lease.Dispose();
                Assert.DoesNotThrow(() => lease.Dispose());
            }
            finally { pool.Dispose(); }
        }
    }
}
