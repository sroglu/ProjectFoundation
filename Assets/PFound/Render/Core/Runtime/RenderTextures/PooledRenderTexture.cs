using UnityEngine;

namespace PFound.Render.Core.RenderTextures
{
    /// <summary>
    /// Internal pool entry. One per allocated <see cref="UnityEngine.RenderTexture"/>.
    /// </summary>
    internal sealed class PooledRenderTexture
    {
        public RenderTexture RT;
        public RenderTextureKey Key;
        public int Token;
        public bool IsLeased;
        public int LeasedFrame;
        public int LastReleasedFrame;
        public bool LeakReported;
    }
}
