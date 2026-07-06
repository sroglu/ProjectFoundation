using UnityEngine;

namespace PFound.UISystem.Editor.ColorGeneration
{
    /// <summary>
    /// Generates the M3 tonal palette from a seed HCT color.
    ///
    /// A tonal palette is a set of colors sharing the same hue and chroma
    /// but spanning perceptual tones 0 → 100 (black → white).
    ///
    /// M3 uses six tonal palettes:
    ///   Primary, Secondary, Tertiary, Error, Neutral, NeutralVariant
    ///
    /// Reference: https://m3.material.io/styles/color/the-color-system/key-colors-tones
    /// </summary>
    public sealed class TonalPalette
    {
        // Standard M3 tone stops used for role assignment
        private static readonly float[] Tones =
        {
            0f, 10f, 20f, 25f, 30f, 35f, 40f, 50f,
            60f, 70f, 80f, 90f, 95f, 98f, 99f, 100f
        };

        private readonly HctColor _key;

        public TonalPalette(HctColor key)
        {
            _key = key;
        }

        /// <summary>Returns the Color at the given tone (0 = black, 100 = white).</summary>
        public Color Tone(float tone) => _key.WithTone(tone).ToColor();

        // ------------------------------------------------------------------ //
        //  M3 Light Scheme Role Mapping                                       //
        // ------------------------------------------------------------------ //

        // These maps the M3 tonal palette roles to tone values per M3 spec.
        // Reference: https://m3.material.io/styles/color/the-color-system/color-roles

        // Primary tonal palette
        public Color Primary              => Tone(40f);
        public Color OnPrimary            => Tone(100f);
        public Color PrimaryContainer     => Tone(90f);
        public Color OnPrimaryContainer   => Tone(10f);

        // — Dark variants —
        public Color PrimaryDark            => Tone(80f);
        public Color OnPrimaryDark          => Tone(20f);
        public Color PrimaryContainerDark   => Tone(30f);
        public Color OnPrimaryContainerDark => Tone(90f);

        // ------------------------------------------------------------------ //
        //  Factory — create common palettes from a seed Color                 //
        // ------------------------------------------------------------------ //

        /// <summary>Creates a primary tonal palette from a Unity Color seed.</summary>
        public static TonalPalette FromColor(Color seedColor)
            => new TonalPalette(HctColor.FromColor(seedColor));

        /// <summary>Creates a secondary palette (same hue, reduced chroma ≈ seed/3).</summary>
        public static TonalPalette SecondaryFromColor(Color seedColor)
        {
            var hct = HctColor.FromColor(seedColor);
            return new TonalPalette(new HctColor(hct.Hue, hct.Chroma / 3f, 40f));
        }

        /// <summary>Creates a tertiary palette (hue rotated +60°, reduced chroma).</summary>
        public static TonalPalette TertiaryFromColor(Color seedColor)
        {
            var hct = HctColor.FromColor(seedColor);
            return new TonalPalette(new HctColor(hct.Hue + 60f, hct.Chroma / 2f, 40f));
        }

        /// <summary>Creates a neutral palette (same hue, very low chroma ≈ 4).</summary>
        public static TonalPalette NeutralFromColor(Color seedColor)
        {
            var hct = HctColor.FromColor(seedColor);
            return new TonalPalette(new HctColor(hct.Hue, 4f, 40f));
        }

        /// <summary>Creates a neutral-variant palette (same hue, low chroma ≈ 8).</summary>
        public static TonalPalette NeutralVariantFromColor(Color seedColor)
        {
            var hct = HctColor.FromColor(seedColor);
            return new TonalPalette(new HctColor(hct.Hue, 8f, 40f));
        }

        /// <summary>Error palette — fixed hue 25 (M3 red), chroma 84.</summary>
        public static TonalPalette Error()
            => new TonalPalette(new HctColor(25f, 84f, 40f));
    }
}
