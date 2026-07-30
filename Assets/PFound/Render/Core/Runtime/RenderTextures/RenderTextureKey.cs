using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace PFound.Render.Core.RenderTextures
{
    /// <summary>
    /// Identifies a transient <see cref="UnityEngine.RenderTexture"/> class in
    /// <see cref="RenderTexturePool"/>. Two leases with the same key share the
    /// same pooled RT slot.
    /// </summary>
    /// <remarks>
    /// Equality is structural across all fields. <see cref="GetHashCode"/> uses an
    /// FNV1a-style combination. Validation runs in the constructor — invalid keys
    /// throw <see cref="ArgumentOutOfRangeException"/>.
    /// </remarks>
    public readonly struct RenderTextureKey : IEquatable<RenderTextureKey>
    {
        public readonly int Width;
        public readonly int Height;
        public readonly GraphicsFormat Format;
        public readonly int DepthBits;
        public readonly int MSAA;
        public readonly bool HDR;

        public RenderTextureKey(int width, int height, GraphicsFormat format,
            int depthBits = 0, int msaa = 1, bool hdr = false)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (depthBits != 0 && depthBits != 16 && depthBits != 24 && depthBits != 32)
                throw new ArgumentOutOfRangeException(nameof(depthBits), "Must be one of {0, 16, 24, 32}.");
            if (msaa != 1 && msaa != 2 && msaa != 4 && msaa != 8)
                throw new ArgumentOutOfRangeException(nameof(msaa), "Must be one of {1, 2, 4, 8}.");

            Width = width;
            Height = height;
            Format = format;
            DepthBits = depthBits;
            MSAA = msaa;
            HDR = hdr;
        }

        /// <summary>
        /// Convenience overload that folds <see cref="RenderTextureFormat"/> + <see cref="RenderTextureReadWrite"/>
        /// (Linear / sRGB / Default) into the underlying <see cref="GraphicsFormat"/> via
        /// <see cref="GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat, RenderTextureReadWrite)"/>.
        /// HDR is derived from the resolved format. Linear and sRGB requests for the same logical format
        /// produce distinct keys, so the pool correctly distinguishes them.
        /// </summary>
        public RenderTextureKey(int width, int height,
            RenderTextureFormat format, RenderTextureReadWrite colorSpace,
            int depthBits = 0, int msaa = 1)
            : this(width, height,
                   ResolveFormat(format, colorSpace),
                   depthBits, msaa,
                   GraphicsFormatUtility.IsHDRFormat(ResolveFormat(format, colorSpace)))
        { }

        /// <summary>
        /// Resolves a <see cref="RenderTextureFormat"/> + <see cref="RenderTextureReadWrite"/> to a
        /// concrete <see cref="GraphicsFormat"/>. The <c>(format, RenderTextureReadWrite)</c> overload of
        /// <see cref="GraphicsFormatUtility"/> collapses Linear and sRGB to the same UNorm format, so we
        /// route through the explicit <c>isSRGB</c> overload to keep the two color spaces on distinct keys.
        /// <see cref="RenderTextureReadWrite.Default"/> follows the active project color space.
        /// </summary>
        private static GraphicsFormat ResolveFormat(RenderTextureFormat format, RenderTextureReadWrite colorSpace)
        {
            // GetGraphicsFormat collapses Linear and sRGB requests to the same UNorm format, so we
            // resolve a base format and then explicitly select its linear vs sRGB sibling — these are
            // guaranteed distinct GraphicsFormat values (e.g. R8G8B8A8_UNorm vs R8G8B8A8_SRGB).
            GraphicsFormat baseFormat = GraphicsFormatUtility.GetGraphicsFormat(format, RenderTextureReadWrite.Linear);
            switch (colorSpace)
            {
                case RenderTextureReadWrite.Linear:
                    return GraphicsFormatUtility.GetLinearFormat(baseFormat);
                case RenderTextureReadWrite.sRGB:
                    return GraphicsFormatUtility.GetSRGBFormat(baseFormat);
                default:
                    return QualitySettings.activeColorSpace == ColorSpace.Linear
                        ? GraphicsFormatUtility.GetSRGBFormat(baseFormat)
                        : GraphicsFormatUtility.GetLinearFormat(baseFormat);
            }
        }

        public bool Equals(RenderTextureKey other) =>
            Width == other.Width && Height == other.Height &&
            Format == other.Format && DepthBits == other.DepthBits &&
            MSAA == other.MSAA && HDR == other.HDR;

        public override bool Equals(object obj) => obj is RenderTextureKey k && Equals(k);

        public override int GetHashCode()
        {
            unchecked
            {
                const int prime = 16777619;
                int h = (int)2166136261;
                h = (h ^ Width) * prime;
                h = (h ^ Height) * prime;
                h = (h ^ (int)Format) * prime;
                h = (h ^ DepthBits) * prime;
                h = (h ^ MSAA) * prime;
                h = (h ^ (HDR ? 1 : 0)) * prime;
                return h;
            }
        }

        public override string ToString() =>
            $"{Width}x{Height} {Format} depth={DepthBits} MSAA={MSAA} HDR={HDR}";

        public static bool operator ==(RenderTextureKey a, RenderTextureKey b) => a.Equals(b);
        public static bool operator !=(RenderTextureKey a, RenderTextureKey b) => !a.Equals(b);
    }
}
