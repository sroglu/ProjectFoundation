using System;
using System.Text;

namespace PFound.Render.UIShapes
{
    /// <summary>
    /// Bit-flags enum representing which optional effects are enabled on a
    /// <c>Render/UI/Shape</c> material. Used by <see cref="UIShapeEffectComposition"/>
    /// to render the documented composition order as a human-readable string.
    /// </summary>
    [Flags]
    public enum EffectMask
    {
        /// <summary>Only fill is rendered.</summary>
        None = 0,
        Gradient = 1 << 0,
        Outline = 1 << 1,
        Banding = 1 << 2,
        Noise = 1 << 3,
        Dots = 1 << 4,
        Shadow = 1 << 5,
    }

    /// <summary>
    /// Helper that returns the documented effect composition order for a given enabled set.
    /// Drives both the Inspector's diagnostic readout + the test suite that pins the order.
    /// </summary>
    /// <remarks>
    /// Composition order is fixed (FR-009):
    /// <c>shadow → fill → gradient → banding → noise → dots → outline</c>.
    /// <para>
    /// - Shadow renders behind the shape (drop-shadow direction).
    /// - Fill is the base color of the interior.
    /// - Gradient REPLACES the fill in the interior when enabled.
    /// - Banding / Noise / Dots OVERLAY the gradient/fill (preserve alpha as masks).
    /// - Outline is the top-most (crisp edge always wins at the SDF zero-isoline).
    /// </para>
    /// </remarks>
    public static class UIShapeEffectComposition
    {
        private const string FillToken = "fill";
        private const string Arrow = " → ";

        /// <summary>
        /// Builds the composition-order string for the given <paramref name="mask"/>.
        /// Always includes the literal token <c>"fill"</c>; the other tokens appear in the fixed
        /// shadow → fill → gradient → banding → noise → dots → outline order, gated by the mask.
        /// </summary>
        /// <param name="mask">Set of enabled effects.</param>
        /// <returns>Human-readable composition order (e.g., <c>"shadow → fill → gradient → outline"</c>).</returns>
        public static string GetCompositionString(EffectMask mask)
        {
            var sb = new StringBuilder(64);

            if ((mask & EffectMask.Shadow) != 0)
            {
                sb.Append("shadow").Append(Arrow);
            }

            sb.Append(FillToken);

            if ((mask & EffectMask.Gradient) != 0)
            {
                sb.Append(Arrow).Append("gradient");
            }

            if ((mask & EffectMask.Banding) != 0)
            {
                sb.Append(Arrow).Append("banding");
            }

            if ((mask & EffectMask.Noise) != 0)
            {
                sb.Append(Arrow).Append("noise");
            }

            if ((mask & EffectMask.Dots) != 0)
            {
                sb.Append(Arrow).Append("dots");
            }

            if ((mask & EffectMask.Outline) != 0)
            {
                sb.Append(Arrow).Append("outline");
            }

            return sb.ToString();
        }
    }
}
