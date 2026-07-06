using System;
using UnityEngine;

namespace PFound.Render.Utilities
{
    /// <summary>
    /// Owns a single <see cref="RenderTexture"/> and keeps it matched to a requested
    /// target size. Call <see cref="GetForSize"/> each frame; the backing texture is
    /// only reallocated when the dimensions actually change, so steady-state usage is
    /// allocation-free. Dispose to release the GPU resource.
    /// </summary>
    public sealed class AutoSizedRenderTexture : IDisposable
    {
        readonly int depthBits;
        readonly RenderTextureFormat format;

        RenderTexture texture;

        /// <summary>The current backing texture, or null before the first request.</summary>
        public RenderTexture Current => texture;

        public AutoSizedRenderTexture(int depthBits = 24, RenderTextureFormat format = RenderTextureFormat.Default)
        {
            this.depthBits = depthBits;
            this.format = format;
        }

        /// <summary>
        /// Returns a render texture of exactly <paramref name="width"/> ×
        /// <paramref name="height"/>, recreating the internal one only if the size
        /// differs from what is already allocated.
        /// </summary>
        public RenderTexture GetForSize(int width, int height)
        {
            if (width < 1) width = 1;
            if (height < 1) height = 1;

            if (texture != null && texture.width == width && texture.height == height)
                return texture;

            Release();

            texture = new RenderTexture(width, height, depthBits, format);
            texture.Create();
            return texture;
        }

        /// <summary>
        /// Convenience overload that sizes the render texture to the camera's current
        /// pixel dimensions.
        /// </summary>
        public RenderTexture GetForCamera(Camera camera)
        {
            return GetForSize(camera.pixelWidth, camera.pixelHeight);
        }

        void Release()
        {
            if (texture == null)
                return;

            if (texture.IsCreated())
                texture.Release();

            // Editor/runtime-safe destruction of the object.
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(texture);
            else
                UnityEngine.Object.DestroyImmediate(texture);

            texture = null;
        }

        public void Dispose()
        {
            Release();
        }
    }
}
