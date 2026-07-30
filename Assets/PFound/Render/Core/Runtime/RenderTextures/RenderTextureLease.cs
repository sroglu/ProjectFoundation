using System;
using UnityEngine;

namespace PFound.Render.Core.RenderTextures
{
    /// <summary>
    /// Handle returned by <see cref="RenderTexturePool.Lease"/>. Disposable so callers
    /// can write <c>using var lease = pool.Lease(key);</c> for scope-bound release.
    /// </summary>
    public struct RenderTextureLease : IDisposable
    {
        public RenderTexture RT { get; private set; }
        public RenderTextureKey Key { get; }
        internal int Token { get; }
        internal RenderTexturePool Owner { get; }

        internal RenderTextureLease(RenderTexture rt, RenderTextureKey key, int token, RenderTexturePool owner)
        {
            RT = rt;
            Key = key;
            Token = token;
            Owner = owner;
        }

        public void Dispose()
        {
            if (Owner == null) return;
            Owner.Release(this);
            RT = null;
        }
    }
}