using System;
using UnityEngine;

namespace PFound.UISystem.Shapes
{
    /// <summary>
    /// Quantized shape geometry identity used as the lookup key for shared materials in
    /// <see cref="SdfShapeMaterials.GetMaterial"/>. Two configs with the same quantized
    /// values are equal (intentional — prevents material proliferation from sub-pixel
    /// float drift like 12.0 vs 12.0000001).
    /// </summary>
    /// <remarks>
    /// Encoding: 18 bytes total, all integers. All float inputs quantize to 0.5px
    /// granularity (× 2, round to int), which is well below display pixel resolution
    /// (~0.25-0.5mm) — invisible to users.
    /// <para>
    /// See spec 009 data-model.md "SdfShapeConfig" entity for the full rationale and the
    /// alternative encodings considered (B-1 naive float, B-3 enum-based).
    /// </para>
    /// </remarks>
    public readonly struct SdfShapeConfig : IEquatable<SdfShapeConfig>
    {
        public readonly ushort CornerRadiusTL;
        public readonly ushort CornerRadiusTR;
        public readonly ushort CornerRadiusBR;
        public readonly ushort CornerRadiusBL;
        public readonly ushort OutlineThickness;
        public readonly ushort ShadowBlur;
        public readonly short  ShadowOffsetX;
        public readonly short  ShadowOffsetY;
        public readonly ushort ShadowPadding;
        // Element size in px (quantized to 0.5px). Drives _RectSize material uniform so
        // the SDF math evaluates a rect of the correct aspect ratio — without this every
        // element of every size would share the same 100×100 SDF stretched to its quad,
        // producing elliptical corners on non-square cards (spec 026 P2 audit finding).
        // Elements of identical size still batch into the same material; elements of
        // different sizes get their own cached category.
        public readonly ushort RectSizeX;
        public readonly ushort RectSizeY;
        // OutlineColor packed as ARGB (Color32) — quantized at 8-bit per channel.
        // M3 components mutate outline color per variant (Filled vs Outlined etc.) so it
        // must drive category selection to keep different variants from sharing a material.
        public readonly Color32 OutlineColor;

        public SdfShapeConfig(
            Vector4 cornerRadii,
            float outlineThickness,
            float shadowBlur,
            Vector2 shadowOffset,
            float shadowPadding,
            Color outlineColor,
            Vector2 rectSize)
        {
            CornerRadiusTL = QuantizeUShort(cornerRadii.x);
            CornerRadiusTR = QuantizeUShort(cornerRadii.y);
            CornerRadiusBR = QuantizeUShort(cornerRadii.z);
            CornerRadiusBL = QuantizeUShort(cornerRadii.w);
            OutlineThickness = QuantizeUShort(outlineThickness);
            ShadowBlur = QuantizeUShort(shadowBlur);
            ShadowOffsetX = QuantizeShort(shadowOffset.x);
            ShadowOffsetY = QuantizeShort(shadowOffset.y);
            ShadowPadding = QuantizeUShort(shadowPadding);
            OutlineColor = (Color32)outlineColor;
            RectSizeX = QuantizeUShort(rectSize.x);
            RectSizeY = QuantizeUShort(rectSize.y);
        }

        /// <summary>Dequantized rect size (W,H) in px — the element's body size for the SDF math.</summary>
        public Vector2 RectSizeVector => new(RectSizeX * 0.5f, RectSizeY * 0.5f);

        /// <summary>True when any shadow parameter is non-zero — drives material category selection.</summary>
        public bool HasShadow => ShadowBlur != 0 || ShadowOffsetX != 0 || ShadowOffsetY != 0;

        /// <summary>Dequantized corner-radii Vector4 (TL, TR, BR, BL) ready for material uniform set.</summary>
        public Vector4 CornerRadiiVector => new(
            CornerRadiusTL * 0.5f,
            CornerRadiusTR * 0.5f,
            CornerRadiusBR * 0.5f,
            CornerRadiusBL * 0.5f);

        public bool Equals(SdfShapeConfig other) =>
            CornerRadiusTL == other.CornerRadiusTL &&
            CornerRadiusTR == other.CornerRadiusTR &&
            CornerRadiusBR == other.CornerRadiusBR &&
            CornerRadiusBL == other.CornerRadiusBL &&
            OutlineThickness == other.OutlineThickness &&
            ShadowBlur == other.ShadowBlur &&
            ShadowOffsetX == other.ShadowOffsetX &&
            ShadowOffsetY == other.ShadowOffsetY &&
            ShadowPadding == other.ShadowPadding &&
            RectSizeX == other.RectSizeX &&
            RectSizeY == other.RectSizeY &&
            OutlineColor.r == other.OutlineColor.r &&
            OutlineColor.g == other.OutlineColor.g &&
            OutlineColor.b == other.OutlineColor.b &&
            OutlineColor.a == other.OutlineColor.a;

        public override bool Equals(object obj) => obj is SdfShapeConfig other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + CornerRadiusTL;
                h = h * 31 + CornerRadiusTR;
                h = h * 31 + CornerRadiusBR;
                h = h * 31 + CornerRadiusBL;
                h = h * 31 + OutlineThickness;
                h = h * 31 + ShadowBlur;
                h = h * 31 + ShadowOffsetX;
                h = h * 31 + ShadowOffsetY;
                h = h * 31 + ShadowPadding;
                h = h * 31 + RectSizeX;
                h = h * 31 + RectSizeY;
                h = h * 31 + (OutlineColor.r | (OutlineColor.g << 8) | (OutlineColor.b << 16) | (OutlineColor.a << 24));
                return h;
            }
        }

        public static bool operator ==(SdfShapeConfig a, SdfShapeConfig b) => a.Equals(b);
        public static bool operator !=(SdfShapeConfig a, SdfShapeConfig b) => !a.Equals(b);

        private static ushort QuantizeUShort(float v) =>
            (ushort)Mathf.Clamp(Mathf.RoundToInt(v * 2f), 0, ushort.MaxValue);

        private static short QuantizeShort(float v) =>
            (short)Mathf.Clamp(Mathf.RoundToInt(v * 2f), short.MinValue, short.MaxValue);
    }
}
