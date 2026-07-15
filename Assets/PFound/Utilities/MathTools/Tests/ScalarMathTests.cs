using System;

namespace PFound.Utilities.MathTools.Tests
{
    internal static class ScalarMathTests
    {
        public static void Run()
        {
            Console.WriteLine("ScalarMath");

            // Remap
            TestHarness.Near(ScalarMath.Remap(5f, 0f, 10f, 0f, 100f), 50f, "Remap midpoint");
            TestHarness.Near(ScalarMath.Remap(0f, -1f, 1f, 10f, 20f), 15f, "Remap centered");
            TestHarness.Near(ScalarMath.RemapClamped(20f, 0f, 10f, 0f, 100f), 100f, "RemapClamped above");
            TestHarness.Near(ScalarMath.RemapClamped(-5f, 0f, 10f, 0f, 100f), 0f, "RemapClamped below");
            TestHarness.Near(ScalarMath.RemapClamped(20f, 0f, 10f, 100f, 0f), 0f, "RemapClamped inverted out");

            // Clamp
            TestHarness.Near(ScalarMath.Clamp01(1.5f), 1f, "Clamp01 high");
            TestHarness.Near(ScalarMath.Clamp01(-0.5f), 0f, "Clamp01 low");
            TestHarness.Near(ScalarMath.Clamp01(0.3f), 0.3f, "Clamp01 mid");
            TestHarness.Near(ScalarMath.ClampSigned(-2f), -1f, "ClampSigned low");
            TestHarness.Near(ScalarMath.ClampSigned(2f), 1f, "ClampSigned high");

            // Wrap
            TestHarness.Near(ScalarMath.Wrap01(1.25f), 0.25f, "Wrap01 over");
            TestHarness.Near(ScalarMath.Wrap01(-0.25f), 0.75f, "Wrap01 under");
            TestHarness.Near(ScalarMath.Wrap(11f, 0f, 10f), 1f, "Wrap range");
            TestHarness.Near(ScalarMath.WrapRadiansSigned((float)(Math.PI * 3)),
                (float)(-Math.PI), "WrapRadiansSigned to -PI", 1e-3);
            TestHarness.That(ScalarMath.WrapRadiansPositive((float)(-Math.PI * 0.5)) > 0f,
                "WrapRadiansPositive stays positive");

            // Tolerance comparisons
            TestHarness.That(ScalarMath.IsNearZero(1e-6f), "IsNearZero true");
            TestHarness.That(!ScalarMath.IsNearZero(0.1f), "IsNearZero false");
            TestHarness.That(ScalarMath.Approximately(1.0000001f, 1f), "Approximately true");
            TestHarness.That(ScalarMath.IsBetween(5f, 0f, 10f), "IsBetween inclusive");
            TestHarness.That(ScalarMath.IsBetween(10f, 0f, 10f), "IsBetween edge inclusive");
            TestHarness.That(!ScalarMath.IsBetweenExclusive(10f, 0f, 10f), "IsBetweenExclusive edge");

            // Rounding / snapping
            TestHarness.Near(ScalarMath.RoundToDecimals(3.14159f, 2), 3.14f, "RoundToDecimals");
            TestHarness.Near(ScalarMath.SnapToStep(7f, 5f), 5f, "SnapToStep down");
            TestHarness.Near(ScalarMath.SnapToStep(8f, 5f), 10f, "SnapToStep up");
            TestHarness.That(ScalarMath.IsSnapped(10f, 5f), "IsSnapped true");
            TestHarness.That(!ScalarMath.IsSnapped(7f, 5f), "IsSnapped false");

            // Sign-preserving powers
            TestHarness.Near(ScalarMath.SignedSqrt(-9f), -3f, "SignedSqrt negative");
            TestHarness.Near(ScalarMath.SignedSqrt(9f), 3f, "SignedSqrt positive");
            TestHarness.Near(ScalarMath.SignedSquare(-3f), -9f, "SignedSquare negative");
            TestHarness.Near(ScalarMath.SignedPow(-2f, 3f), -8f, "SignedPow negative");

            // Misc
            TestHarness.That(ScalarMath.DigitCount(0) == 1, "DigitCount zero");
            TestHarness.That(ScalarMath.DigitCount(-12345) == 5, "DigitCount negative");
            TestHarness.That(ScalarMath.DigitCount(1000) == 4, "DigitCount thousand");
            TestHarness.That(ScalarMath.IsPowerOfTwo(64), "IsPowerOfTwo 64");
            TestHarness.That(!ScalarMath.IsPowerOfTwo(48), "IsPowerOfTwo 48 false");
            TestHarness.That(!ScalarMath.IsPowerOfTwo(0), "IsPowerOfTwo 0 false");
            TestHarness.Near(ScalarMath.ZeroIfNaN(float.NaN), 0f, "ZeroIfNaN nan");
            TestHarness.Near(ScalarMath.ZeroIfNaN(4f), 4f, "ZeroIfNaN value");

            // Time
            TestHarness.Near(ScalarMath.SecondsToMilliseconds(2f), 2000f, "SecondsToMilliseconds");
            TestHarness.Near(ScalarMath.MillisecondsToSeconds(500f), 0.5f, "MillisecondsToSeconds");
        }
    }
}
