using System;
using System.Globalization;
using UnityEngine;

namespace PFound.Utilities.ColorTools
{
    /// <summary>
    /// Extension and helper methods for <see cref="Color"/> and <see cref="Color32"/>:
    /// exact / approximate comparison, packing and format conversions, fast interpolation,
    /// HSL colour-space conversion, brightness adjustment, hexadecimal parsing/formatting
    /// and alpha replacement.
    ///
    /// HSL components use the normalised <c>[0, 1]</c> range for hue, saturation and lightness,
    /// matching the convention Unity uses for its built-in HSV helpers.
    /// </summary>
    public static class ColorTools
    {
        // ---------------------------------------------------------------------
        // Equality
        // ---------------------------------------------------------------------

        /// <summary>Exact component-wise equality across all four channels (R, G, B, A).</summary>
        public static bool EqualsRgba(this Color a, Color b)
        {
            return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
        }

        /// <summary>Exact component-wise equality across the three colour channels, ignoring alpha.</summary>
        public static bool EqualsRgb(this Color a, Color b)
        {
            return a.r == b.r && a.g == b.g && a.b == b.b;
        }

        /// <summary>All four channels equal to within <paramref name="tolerance"/>.</summary>
        public static bool ApproximatelyEqualsRgba(this Color a, Color b, float tolerance = 0.001f)
        {
            return Math.Abs(a.r - b.r) <= tolerance
                && Math.Abs(a.g - b.g) <= tolerance
                && Math.Abs(a.b - b.b) <= tolerance
                && Math.Abs(a.a - b.a) <= tolerance;
        }

        /// <summary>The three colour channels equal to within <paramref name="tolerance"/>, ignoring alpha.</summary>
        public static bool ApproximatelyEqualsRgb(this Color a, Color b, float tolerance = 0.001f)
        {
            return Math.Abs(a.r - b.r) <= tolerance
                && Math.Abs(a.g - b.g) <= tolerance
                && Math.Abs(a.b - b.b) <= tolerance;
        }

        /// <summary>The alpha channels equal to within <paramref name="tolerance"/>.</summary>
        public static bool ApproximatelyEqualsAlpha(this Color a, Color b, float tolerance = 0.001f)
        {
            return Math.Abs(a.a - b.a) <= tolerance;
        }

        // ---------------------------------------------------------------------
        // Packed 32-bit integer conversion (0xRRGGBBAA byte layout)
        // ---------------------------------------------------------------------

        /// <summary>
        /// Packs the colour into a 32-bit integer with an <c>R,G,B,A</c> byte layout
        /// (red in the most-significant byte). Each channel is rounded to the nearest byte.
        /// </summary>
        public static int ToInt32(this Color color)
        {
            return ((Color32)color).ToInt32();
        }

        /// <summary>Packs the colour into a 32-bit integer with an <c>R,G,B,A</c> byte layout.</summary>
        public static int ToInt32(this Color32 color)
        {
            return (color.r << 24) | (color.g << 16) | (color.b << 8) | color.a;
        }

        /// <summary>Rebuilds a <see cref="Color32"/> from a 32-bit integer packed as <c>R,G,B,A</c>.</summary>
        public static Color32 ToColor32(int packed)
        {
            return new Color32(
                (byte)((packed >> 24) & 0xFF),
                (byte)((packed >> 16) & 0xFF),
                (byte)((packed >> 8) & 0xFF),
                (byte)(packed & 0xFF));
        }

        /// <summary>Rebuilds a floating-point <see cref="Color"/> from a 32-bit integer packed as <c>R,G,B,A</c>.</summary>
        public static Color ToColor(int packed)
        {
            return ToColor32(packed);
        }

        // ---------------------------------------------------------------------
        // Color <-> Color32
        // ---------------------------------------------------------------------

        /// <summary>Converts a floating-point colour to its 8-bit-per-channel representation.</summary>
        public static Color32 ToColor32(this Color color)
        {
            return color; // Unity provides an implicit narrowing conversion.
        }

        /// <summary>Converts an 8-bit-per-channel colour to its floating-point representation.</summary>
        public static Color ToColor(this Color32 color)
        {
            return color; // Unity provides an implicit widening conversion.
        }

        // ---------------------------------------------------------------------
        // Fast (unclamped) interpolation
        // ---------------------------------------------------------------------

        /// <summary>
        /// Linearly interpolates each channel without clamping <paramref name="t"/> to <c>[0, 1]</c>.
        /// </summary>
        public static Color FastLerp(this Color a, Color b, float t)
        {
            return new Color(
                a.r + (b.r - a.r) * t,
                a.g + (b.g - a.g) * t,
                a.b + (b.b - a.b) * t,
                a.a + (b.a - a.a) * t);
        }

        /// <summary>
        /// Linearly interpolates each channel in 8-bit space without clamping <paramref name="t"/>.
        /// </summary>
        public static Color32 FastLerp(this Color32 a, Color32 b, float t)
        {
            return new Color32(
                (byte)(a.r + (b.r - a.r) * t),
                (byte)(a.g + (b.g - a.g) * t),
                (byte)(a.b + (b.b - a.b) * t),
                (byte)(a.a + (b.a - a.a) * t));
        }

        // ---------------------------------------------------------------------
        // HSL <-> RGB
        // ---------------------------------------------------------------------

        /// <summary>
        /// Decomposes the colour into hue, saturation and lightness, each normalised to <c>[0, 1]</c>.
        /// Alpha is ignored.
        /// </summary>
        public static void ToHsl(this Color color, out float hue, out float saturation, out float lightness)
        {
            float r = color.r, g = color.g, b = color.b;
            float max = Mathf.Max(r, Mathf.Max(g, b));
            float min = Mathf.Min(r, Mathf.Min(g, b));

            lightness = (max + min) * 0.5f;

            float delta = max - min;
            if (delta <= Mathf.Epsilon)
            {
                hue = 0f;
                saturation = 0f;
                return;
            }

            saturation = lightness > 0.5f
                ? delta / (2f - max - min)
                : delta / (max + min);

            float h;
            if (max == r)
                h = (g - b) / delta + (g < b ? 6f : 0f);
            else if (max == g)
                h = (b - r) / delta + 2f;
            else
                h = (r - g) / delta + 4f;

            hue = h / 6f;
        }

        /// <summary>
        /// Builds a colour from hue, saturation and lightness (each in <c>[0, 1]</c>) and an alpha value.
        /// </summary>
        public static Color FromHsl(float hue, float saturation, float lightness, float alpha = 1f)
        {
            if (saturation <= 0f)
                return new Color(lightness, lightness, lightness, alpha);

            float q = lightness < 0.5f
                ? lightness * (1f + saturation)
                : lightness + saturation - lightness * saturation;
            float p = 2f * lightness - q;

            float r = HueToChannel(p, q, hue + 1f / 3f);
            float g = HueToChannel(p, q, hue);
            float b = HueToChannel(p, q, hue - 1f / 3f);
            return new Color(r, g, b, alpha);
        }

        private static float HueToChannel(float p, float q, float t)
        {
            if (t < 0f) t += 1f;
            if (t > 1f) t -= 1f;
            if (t < 1f / 6f) return p + (q - p) * 6f * t;
            if (t < 1f / 2f) return q;
            if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
            return p;
        }

        // ---------------------------------------------------------------------
        // Brightness
        // ---------------------------------------------------------------------

        /// <summary>
        /// Scales the colour channels by <paramref name="factor"/> (1 leaves the colour unchanged,
        /// &lt; 1 darkens, &gt; 1 brightens) and clamps the result to <c>[0, 1]</c>. Alpha is preserved.
        /// </summary>
        public static Color AdjustBrightness(this Color color, float factor)
        {
            return new Color(
                Mathf.Clamp01(color.r * factor),
                Mathf.Clamp01(color.g * factor),
                Mathf.Clamp01(color.b * factor),
                color.a);
        }

        // ---------------------------------------------------------------------
        // Hexadecimal
        // ---------------------------------------------------------------------

        /// <summary>
        /// Formats the colour as <c>#RRGGBB</c>, or <c>#RRGGBBAA</c> when
        /// <paramref name="includeAlpha"/> is <see langword="true"/>.
        /// </summary>
        public static string ToHexString(this Color color, bool includeAlpha = false)
        {
            return ((Color32)color).ToHexString(includeAlpha);
        }

        /// <summary>
        /// Formats the colour as <c>#RRGGBB</c>, or <c>#RRGGBBAA</c> when
        /// <paramref name="includeAlpha"/> is <see langword="true"/>.
        /// </summary>
        public static string ToHexString(this Color32 color, bool includeAlpha = false)
        {
            return includeAlpha
                ? string.Format("#{0:X2}{1:X2}{2:X2}{3:X2}", color.r, color.g, color.b, color.a)
                : string.Format("#{0:X2}{1:X2}{2:X2}", color.r, color.g, color.b);
        }

        /// <summary>
        /// Parses a <c>#RRGGBB</c> or <c>#RRGGBBAA</c> string (the leading <c>#</c> is optional).
        /// Throws <see cref="FormatException"/> when the input is malformed.
        /// </summary>
        public static Color ParseHex(string hex)
        {
            if (TryParseHex(hex, out Color color))
                return color;
            throw new FormatException("Not a valid #RRGGBB or #RRGGBBAA colour string: '" + hex + "'.");
        }

        /// <summary>
        /// Attempts to parse a <c>#RRGGBB</c> or <c>#RRGGBBAA</c> string (the leading <c>#</c> is optional).
        /// </summary>
        public static bool TryParseHex(string hex, out Color color)
        {
            if (TryParseHex32(hex, out Color32 c))
            {
                color = c;
                return true;
            }
            color = default;
            return false;
        }

        /// <summary>
        /// Attempts to parse a <c>#RRGGBB</c> or <c>#RRGGBBAA</c> string into a <see cref="Color32"/>
        /// (the leading <c>#</c> is optional).
        /// </summary>
        public static bool TryParseHex32(string hex, out Color32 color)
        {
            color = default;
            if (string.IsNullOrEmpty(hex))
                return false;

            int start = hex[0] == '#' ? 1 : 0;
            int length = hex.Length - start;
            if (length != 6 && length != 8)
                return false;

            if (!TryReadByte(hex, start, out byte r) ||
                !TryReadByte(hex, start + 2, out byte g) ||
                !TryReadByte(hex, start + 4, out byte b))
                return false;

            byte a = 255;
            if (length == 8 && !TryReadByte(hex, start + 6, out a))
                return false;

            color = new Color32(r, g, b, a);
            return true;
        }

        private static bool TryReadByte(string text, int index, out byte value)
        {
            return byte.TryParse(
                text.Substring(index, 2),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out value);
        }

        // ---------------------------------------------------------------------
        // With-alpha
        // ---------------------------------------------------------------------

        /// <summary>Returns a copy of the colour with its alpha replaced by <paramref name="alpha"/>.</summary>
        public static Color WithAlpha(this Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        /// <summary>Returns a copy of the colour with its alpha replaced by <paramref name="alpha"/>.</summary>
        public static Color32 WithAlpha(this Color32 color, byte alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
