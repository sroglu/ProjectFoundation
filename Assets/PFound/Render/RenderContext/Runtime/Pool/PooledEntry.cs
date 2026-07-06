using UnityEngine;

namespace PFound.Render.RenderContext
{
    internal struct PooledEntry
    {
        /// <summary>Lease obtained from <c>PFound.Render.Core.RenderTextures.RenderTexturePool</c>.</summary>
        public PFound.Render.Core.RenderTextures.RenderTextureLease Lease;
        /// <summary>Convenience cache of <c>Lease.RT</c>.</summary>
        public RenderTexture Rt;
        /// <summary>Wrapper GameObject (root of the per-entry hierarchy) — Camera + ContentRoot are sibling children of this.</summary>
        public GameObject Root;
        public Camera Camera;
        public Transform ContentRoot;
        public RenderContextPoolKey Key;
    }
}
