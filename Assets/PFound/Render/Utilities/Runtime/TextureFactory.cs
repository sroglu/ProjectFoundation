using UnityEngine;

namespace PFound.Render.Utilities
{
    /// <summary>
    /// Builds <see cref="Texture2D"/> assets procedurally (solid fills, gradients,
    /// per-pixel tints) and produces CPU-readable copies of textures that may live
    /// GPU-side only, optionally rescaling them along the way.
    /// </summary>
    public static class TextureFactory
    {
        /// <summary>Direction a linear gradient runs across a generated texture.</summary>
        public enum GradientAxis
        {
            /// <summary>Colour changes left-to-right along the width.</summary>
            Horizontal,
            /// <summary>Colour changes bottom-to-top along the height.</summary>
            Vertical
        }

        /// <summary>
        /// Creates a texture filled uniformly with a single colour.
        /// </summary>
        public static Texture2D CreateSolidTexture(int width, int height, Color color)
        {
            var texture = NewTexture(width, height);
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// Creates a texture whose colour interpolates from <paramref name="from"/>
        /// to <paramref name="to"/> along the height (bottom to top).
        /// </summary>
        public static Texture2D CreateVerticalGradient(int width, int height, Color from, Color to)
        {
            return CreateGradient(width, height, from, to, GradientAxis.Vertical);
        }

        /// <summary>
        /// Creates a texture whose colour interpolates from <paramref name="from"/>
        /// to <paramref name="to"/> along the width (left to right).
        /// </summary>
        public static Texture2D CreateHorizontalGradient(int width, int height, Color from, Color to)
        {
            return CreateGradient(width, height, from, to, GradientAxis.Horizontal);
        }

        static Texture2D CreateGradient(int width, int height, Color from, Color to, GradientAxis axis)
        {
            var texture = NewTexture(width, height);
            var pixels = new Color[width * height];

            int span = axis == GradientAxis.Vertical ? height : width;
            // Guard against a 1px span producing a divide-by-zero.
            float denom = span > 1 ? span - 1 : 1;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int step = axis == GradientAxis.Vertical ? y : x;
                    float t = step / denom;
                    pixels[y * width + x] = Color.Lerp(from, to, t);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// Returns a new texture that is <paramref name="source"/> with every pixel
        /// multiplied by <paramref name="tint"/>. The source must be readable.
        /// </summary>
        public static Texture2D TintTexture(Texture2D source, Color tint)
        {
            var pixels = source.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] *= tint;

            var texture = NewTexture(source.width, source.height);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// Copies any texture (including non-readable / compressed ones) into a fresh
        /// CPU-readable <see cref="Texture2D"/> at its original size, using a GPU blit
        /// through a temporary <see cref="RenderTexture"/>.
        /// </summary>
        public static Texture2D CreateReadableCopy(Texture source)
        {
            return CreateReadableCopy(source, source.width, source.height);
        }

        /// <summary>
        /// Copies any texture into a fresh CPU-readable <see cref="Texture2D"/> while
        /// rescaling it to the requested dimensions. The blit does the resampling on
        /// the GPU, so the source does not need to be readable.
        /// </summary>
        public static Texture2D CreateReadableCopy(Texture source, int targetWidth, int targetHeight)
        {
            var temp = RenderTexture.GetTemporary(
                targetWidth,
                targetHeight,
                0,
                RenderTextureFormat.Default,
                RenderTextureReadWrite.Default);

            var previouslyActive = RenderTexture.active;
            try
            {
                Graphics.Blit(source, temp);
                RenderTexture.active = temp;

                var readable = NewTexture(targetWidth, targetHeight);
                readable.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
                readable.Apply();
                return readable;
            }
            finally
            {
                RenderTexture.active = previouslyActive;
                RenderTexture.ReleaseTemporary(temp);
            }
        }

        static Texture2D NewTexture(int width, int height)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false);
        }
    }
}
