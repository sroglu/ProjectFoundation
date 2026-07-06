using System;
using UnityEngine;

namespace PFound.Render.RenderContext
{
    /// <summary>
    /// Consumer-authored value type passed by-value into <see cref="IRenderContextService.Acquire"/>.
    /// Equatable for pool-key derivation. See <c>specs/022-render-context/data-model.md</c>.
    /// </summary>
    [Serializable]
    public struct RenderContextDescriptor : IEquatable<RenderContextDescriptor>
    {
        public int Width;
        public int Height;
        public RenderTextureFormat Format;
        public int DepthBits;
        public int Msaa;
        public RenderTextureReadWrite ColorSpace;
        public LayerMask CullingMask;
        public CameraClearFlags ClearFlags;
        public Color BackgroundColor;
        public bool Orthographic;
        public float OrthographicSize;
        public float FieldOfView;

        public static RenderContextDescriptor Default => new RenderContextDescriptor
        {
            Width = 0,
            Height = 0,
            Format = RenderTextureFormat.ARGB32,
            DepthBits = 16,
            Msaa = 1,
            ColorSpace = RenderTextureReadWrite.Default,
            CullingMask = ~0,
            ClearFlags = CameraClearFlags.SolidColor,
            BackgroundColor = new Color(0f, 0f, 0f, 0f),
            Orthographic = false,
            OrthographicSize = 5f,
            FieldOfView = 60f,
        };

        internal void Validate()
        {
            if (Width < 0)
                throw new ArgumentException("RenderContextDescriptor.Width must be >= 0.", nameof(Width));
            if (Height < 0)
                throw new ArgumentException("RenderContextDescriptor.Height must be >= 0.", nameof(Height));

            if (DepthBits != 0 && DepthBits != 16 && DepthBits != 24)
                throw new ArgumentException(
                    $"RenderContextDescriptor.DepthBits must be 0, 16, or 24 (got {DepthBits}).",
                    nameof(DepthBits));

            if (Msaa != 1 && Msaa != 2 && Msaa != 4 && Msaa != 8)
                throw new ArgumentException(
                    $"RenderContextDescriptor.Msaa must be 1, 2, 4, or 8 (got {Msaa}).",
                    nameof(Msaa));

            if (Orthographic)
            {
                if (!(OrthographicSize > 0f))
                    throw new ArgumentException(
                        $"RenderContextDescriptor.OrthographicSize must be > 0 when Orthographic is true (got {OrthographicSize}).",
                        nameof(OrthographicSize));
            }
            else
            {
                if (!(FieldOfView > 0f && FieldOfView < 180f))
                    throw new ArgumentException(
                        $"RenderContextDescriptor.FieldOfView must be in (0, 180) when Orthographic is false (got {FieldOfView}).",
                        nameof(FieldOfView));
            }
        }

        public bool Equals(RenderContextDescriptor other)
            => Width == other.Width
            && Height == other.Height
            && Format == other.Format
            && DepthBits == other.DepthBits
            && Msaa == other.Msaa
            && ColorSpace == other.ColorSpace
            && CullingMask.value == other.CullingMask.value
            && ClearFlags == other.ClearFlags
            && BackgroundColor.Equals(other.BackgroundColor)
            && Orthographic == other.Orthographic
            && OrthographicSize.Equals(other.OrthographicSize)
            && FieldOfView.Equals(other.FieldOfView);

        public override bool Equals(object obj) => obj is RenderContextDescriptor d && Equals(d);

        public override int GetHashCode()
        {
            var h1 = HashCode.Combine(Width, Height, (int)Format, DepthBits, Msaa, (int)ColorSpace);
            var h2 = HashCode.Combine(CullingMask.value, (int)ClearFlags, BackgroundColor.GetHashCode(),
                Orthographic, OrthographicSize, FieldOfView);
            return HashCode.Combine(h1, h2);
        }

        public static bool operator ==(RenderContextDescriptor a, RenderContextDescriptor b) => a.Equals(b);
        public static bool operator !=(RenderContextDescriptor a, RenderContextDescriptor b) => !a.Equals(b);
    }
}
