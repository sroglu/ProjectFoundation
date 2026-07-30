using System;
using UnityEngine;

namespace PFound.Render.Utilities
{
    /// <summary>
    /// Disposable wrapper around the result of <c>TextureResizer.Resize</c>.
    /// Caller's contract is uniform: always <c>using</c> (or otherwise call
    /// <see cref="Dispose"/>) the handle. The internal <see cref="OwnsTexture"/>
    /// flag decides whether <see cref="Dispose"/> destroys the texture or no-ops.
    /// </summary>
    public readonly struct TextureResizeHandle : IDisposable
    {
        /// <summary>The texture to use. May be the original source (pass-through) or a freshly-allocated downscale.</summary>
        public Texture2D Texture { get; }

        /// <summary>When true, <see cref="Dispose"/> destroys <see cref="Texture"/>. When false, it is a no-op.</summary>
        public bool OwnsTexture { get; }

        internal TextureResizeHandle(Texture2D texture, bool ownsTexture)
        {
            Texture = texture;
            OwnsTexture = ownsTexture;
        }

        /// <summary>
        /// Destroys <see cref="Texture"/> when the handle owns it; otherwise no-op.
        /// Idempotent — calling multiple times is safe.
        /// </summary>
        public void Dispose()
        {
            if (!OwnsTexture || Texture == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(Texture);
            else UnityEngine.Object.DestroyImmediate(Texture);
        }
    }
}