using UnityEngine;

namespace PFound.Render.Utilities
{
    /// <summary>
    /// GPU-based texture downscaler. Shrinks a <see cref="Texture2D"/> so its longest
    /// side fits within a maximum dimension while preserving aspect ratio.
    ///
    /// The result is returned inside a <see cref="TextureResizeHandle"/> that carries an
    /// ownership flag: when an actual downscale is produced the handle owns (and on
    /// <see cref="TextureResizeHandle.Dispose"/> destroys) the new texture; when the call
    /// is a pass-through the original is returned and the handle owns nothing.
    /// </summary>
    public static class TextureResizer
    {
        /// <summary>
        /// Returns a texture whose longest side is at most <paramref name="maxDimension"/>.
        /// </summary>
        /// <param name="source">The texture to shrink. May be <c>null</c>.</param>
        /// <param name="maxDimension">The upper bound for the longest side, in pixels.</param>
        /// <returns>
        /// A handle wrapping either a freshly-allocated downscaled copy (owned) or the
        /// original <paramref name="source"/> (not owned) when no work is required.
        /// </returns>
        public static TextureResizeHandle Resize(Texture2D source, int maxDimension)
        {
            // Nothing to work with, or a nonsensical bound: hand the source straight back
            // without taking ownership. No allocation, no GPU work.
            if (source == null || maxDimension <= 0)
                return new TextureResizeHandle(source, false);

            // Already small enough on both axes: pass through untouched.
            if (!TryGetDownscaledSize(source.width, source.height, maxDimension, out int targetWidth, out int targetHeight))
                return new TextureResizeHandle(source, false);

            Texture2D shrunk = Downscale(source, targetWidth, targetHeight);
            return new TextureResizeHandle(shrunk, true);
        }

        /// <summary>
        /// Computes the aspect-preserving target size for a source of the given dimensions.
        /// The longest source axis is mapped onto <paramref name="maxDimension"/> exactly;
        /// the other axis is scaled by the same ratio and floored at one pixel.
        /// </summary>
        /// <returns>
        /// <c>true</c> when a downscale is required (some axis exceeds
        /// <paramref name="maxDimension"/>); <c>false</c> when the source already fits, in
        /// which case the out sizes echo the source.
        /// </returns>
        public static bool TryGetDownscaledSize(int width, int height, int maxDimension, out int targetWidth, out int targetHeight)
        {
            if (width <= maxDimension && height <= maxDimension)
            {
                targetWidth = width;
                targetHeight = height;
                return false;
            }

            // Anchor the longer axis on the bound so it lands on maxDimension precisely,
            // then derive the shorter axis from the same scale factor.
            if (width >= height)
            {
                targetWidth = maxDimension;
                targetHeight = Mathf.Max(1, Mathf.RoundToInt(height * (maxDimension / (float)width)));
            }
            else
            {
                targetHeight = maxDimension;
                targetWidth = Mathf.Max(1, Mathf.RoundToInt(width * (maxDimension / (float)height)));
            }

            return true;
        }

        /// <summary>
        /// Performs the actual GPU downscale: blits <paramref name="source"/> into a
        /// temporary render target of the requested size and reads it back into a new
        /// RGBA32 texture. The previously active render target is always restored.
        /// </summary>
        private static Texture2D Downscale(Texture2D source, int targetWidth, int targetHeight)
        {
            RenderTexture scratch = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, scratch);
                RenderTexture.active = scratch;

                var result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
                result.ReadPixels(new Rect(0f, 0f, targetWidth, targetHeight), 0, 0);
                result.Apply();
                return result;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(scratch);
            }
        }
    }
}
