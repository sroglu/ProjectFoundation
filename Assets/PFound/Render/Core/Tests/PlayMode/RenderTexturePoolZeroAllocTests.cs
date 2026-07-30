using System.Collections;
using NUnit.Framework;
using UnityEngine.Experimental.Rendering;
using UnityEngine.TestTools;
using PFound.Render.Core.RenderTextures;

namespace PFound.Render.Core.Tests
{
    public sealed class RenderTexturePoolZeroAllocTests
    {
        [UnityTest]
        public IEnumerator LeaseReleaseSteadyState_AllocatesZeroBytes()
        {
            var pool = new RenderTexturePool(new RenderTexturePoolOptions()) { LogLeaksToConsole = false };
            var key = new RenderTextureKey(64, 64, GraphicsFormat.R8G8B8A8_UNorm);

            try
            {
                // Pre-warm so first-time RT allocation is amortized.
                for (int i = 0; i < 4; i++)
                {
                    var l = pool.Lease(key);
                    pool.Release(l);
                }

                yield return ZeroAllocAssertions.AssertZeroAlloc(
                    () =>
                    {
                        var l = pool.Lease(key);
                        pool.Release(l);
                    },
                    frameCount: 60,
                    label: "RenderTexturePool steady-state Lease/Release");
            }
            finally { pool.Dispose(); }
        }
    }
}
