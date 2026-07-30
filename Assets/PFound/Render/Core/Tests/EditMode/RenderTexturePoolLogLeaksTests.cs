using NUnit.Framework;
using UnityEngine.Experimental.Rendering;
using UnityEngine.TestTools;
using PFound.Render.Core.RenderTextures;

namespace PFound.Render.Core.Tests
{
    public sealed class RenderTexturePoolLogLeaksTests
    {
        [Test]
        public void LogLeaksToConsole_True_EmitsWarning()
        {
            var options = new RenderTexturePoolOptions(leakFrameThreshold: 3);
            var pool = new RenderTexturePool(options) { LogLeaksToConsole = true };
            try
            {
                var lease = pool.Lease(new RenderTextureKey(64, 64, GraphicsFormat.R8G8B8A8_UNorm));
                int frame = UnityEngine.Time.frameCount;
                LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[RenderTexturePool\] Leaked lease"));
                pool.Tick(frame + 10);
                pool.Release(lease);
            }
            finally { pool.Dispose(); }
        }

        [Test]
        public void LogLeaksToConsole_False_NoWarning()
        {
            var options = new RenderTexturePoolOptions(leakFrameThreshold: 3);
            var pool = new RenderTexturePool(options) { LogLeaksToConsole = false };
            try
            {
                var lease = pool.Lease(new RenderTextureKey(64, 64, GraphicsFormat.R8G8B8A8_UNorm));
                int frame = UnityEngine.Time.frameCount;
                pool.Tick(frame + 10);
                LogAssert.NoUnexpectedReceived();
                pool.Release(lease);
            }
            finally { pool.Dispose(); }
        }
    }
}
