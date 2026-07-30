#if UNITY_INCLUDE_TESTS
using UnityEngine;

namespace PFound.UISystem.Tests.VisualRegression
{
    /// <summary>
    /// Zero-dependency pixel diff helper for spec 009 Phase 3 visual regression tests.
    /// Computes per-channel mean absolute error between two RGBA Texture2D images.
    /// Used to gate per-component migrations against the SC-003 tiered budget:
    /// ≤ 1/255 MAE for shadowless components, ≤ 2/255 MAE for shadowed components
    /// (with per-component PR justification).
    /// </summary>
    /// <remarks>
    /// Same-size constraint: both textures must have identical (width, height). The runner
    /// throws if sizes differ — visual baselines are captured at fixed resolution per
    /// <c>M3VisualBaselineCapture</c> editor menu item, and post-migration captures must
    /// match. No ImageMagick or external native lib needed.
    /// </remarks>
    public static class PixelDiffRunner
    {
        /// <summary>
        /// Mean absolute error per channel (R, G, B, A) between two same-size textures.
        /// Returns the MAX of the four per-channel MAEs, expressed in 0-255 byte range.
        /// </summary>
        /// <exception cref="System.ArgumentException">If sizes mismatch.</exception>
        public static float MeanAbsoluteError(Texture2D a, Texture2D b)
        {
            if (a == null || b == null) throw new System.ArgumentNullException("texture");
            if (a.width != b.width || a.height != b.height)
            {
                throw new System.ArgumentException(
                    $"Texture size mismatch: {a.width}x{a.height} vs {b.width}x{b.height}. " +
                    "Visual baseline + post-migration capture must use identical dimensions.");
            }

            var pixA = a.GetPixels32();
            var pixB = b.GetPixels32();
            long sumR = 0, sumG = 0, sumB = 0, sumA = 0;
            int n = pixA.Length;
            for (int i = 0; i < n; i++)
            {
                sumR += System.Math.Abs(pixA[i].r - pixB[i].r);
                sumG += System.Math.Abs(pixA[i].g - pixB[i].g);
                sumB += System.Math.Abs(pixA[i].b - pixB[i].b);
                sumA += System.Math.Abs(pixA[i].a - pixB[i].a);
            }
            float maeR = sumR / (float)n;
            float maeG = sumG / (float)n;
            float maeB = sumB / (float)n;
            float maeA = sumA / (float)n;
            return Mathf.Max(Mathf.Max(maeR, maeG), Mathf.Max(maeB, maeA));
        }

        /// <summary>
        /// Per-channel MAE breakdown (R, G, B, A separately) — useful for debugging when a
        /// component fails the tier budget and the failing channel needs to be identified.
        /// </summary>
        public static (float R, float G, float B, float A) MeanAbsoluteErrorPerChannel(Texture2D a, Texture2D b)
        {
            if (a == null || b == null) throw new System.ArgumentNullException("texture");
            if (a.width != b.width || a.height != b.height)
            {
                throw new System.ArgumentException(
                    $"Texture size mismatch: {a.width}x{a.height} vs {b.width}x{b.height}.");
            }

            var pixA = a.GetPixels32();
            var pixB = b.GetPixels32();
            long sumR = 0, sumG = 0, sumB = 0, sumA = 0;
            int n = pixA.Length;
            for (int i = 0; i < n; i++)
            {
                sumR += System.Math.Abs(pixA[i].r - pixB[i].r);
                sumG += System.Math.Abs(pixA[i].g - pixB[i].g);
                sumB += System.Math.Abs(pixA[i].b - pixB[i].b);
                sumA += System.Math.Abs(pixA[i].a - pixB[i].a);
            }
            return (sumR / (float)n, sumG / (float)n, sumB / (float)n, sumA / (float)n);
        }
    }
}
#endif
