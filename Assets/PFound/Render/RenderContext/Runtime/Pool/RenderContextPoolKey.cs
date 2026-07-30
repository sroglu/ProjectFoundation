using System;
using UnityEngine;

namespace PFound.Render.RenderContext
{
    /// <summary>
    /// Internal pool dictionary key. Six fields per research Item 2 — only those that bake into RT allocation.
    /// </summary>
    internal readonly struct RenderContextPoolKey : IEquatable<RenderContextPoolKey>
    {
        public readonly int Width;
        public readonly int Height;
        public readonly RenderTextureFormat Format;
        public readonly int DepthBits;
        public readonly int Msaa;
        public readonly RenderTextureReadWrite ColorSpace;

        public RenderContextPoolKey(int width, int height, RenderTextureFormat format, int depthBits, int msaa, RenderTextureReadWrite colorSpace)
        {
            Width = width;
            Height = height;
            Format = format;
            DepthBits = depthBits;
            Msaa = msaa;
            ColorSpace = colorSpace;
        }

        public static RenderContextPoolKey FromDescriptor(in RenderContextDescriptor desc, int resolvedWidth, int resolvedHeight)
            => new RenderContextPoolKey(resolvedWidth, resolvedHeight, desc.Format, desc.DepthBits, desc.Msaa, desc.ColorSpace);

        public bool Equals(RenderContextPoolKey other)
            => Width == other.Width
            && Height == other.Height
            && Format == other.Format
            && DepthBits == other.DepthBits
            && Msaa == other.Msaa
            && ColorSpace == other.ColorSpace;

        public override bool Equals(object obj) => obj is RenderContextPoolKey k && Equals(k);

        public override int GetHashCode() => HashCode.Combine(Width, Height, (int)Format, DepthBits, Msaa, (int)ColorSpace);

        public override string ToString() => $"({Width}×{Height} {Format} d{DepthBits} msaa{Msaa} {ColorSpace})";
    }
}
