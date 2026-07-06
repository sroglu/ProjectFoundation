using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.TestTools;
using PFound.Render.Core.RenderTextures;

namespace PFound.Render.Core.Tests
{
    public sealed class RenderTexturePoolLeakZeroAllocTests
    {
        [UnityTest]
        public IEnumerator LeakWritePath_AllocatesZeroBytes_WhenConsoleMirrorOff()
        {
            var options = new RenderTexturePoolOptions(leakFrameThreshold: 1, leakRingBufferCapacity: 16);
            var pool = new RenderTexturePool(options) { LogLeaksToConsole = false };
            var key = new RenderTextureKey(64, 64, GraphicsFormat.R8G8B8A8_UNorm);

            try
            {
                // Pre-warm one leak so the underlying ring buffer is fully allocated.
                pool.Lease(key);
                pool.Tick(Time.frameCount + 100);
                while (pool.TryReadLeak(out _)) { }

                yield return ZeroAllocAssertions.AssertZeroAlloc(
                    () =>
                    {
                        pool.Lease(key);
                        pool.Tick(Time.frameCount + 100);
                        while (pool.TryReadLeak(out _)) { }
                    },
                    frameCount: 30,
                    label: "RenderTexturePool leak-write path");
            }
            finally { pool.Dispose(); }
        }
    }
}
