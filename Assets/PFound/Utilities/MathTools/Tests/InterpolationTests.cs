using System;

namespace PFound.Utilities.MathTools.Tests
{
    internal static class InterpolationTests
    {
        public static void Run()
        {
            Console.WriteLine("Interpolation");

            // SmoothStep endpoints and midpoint
            TestHarness.Near(Interpolation.SmoothStep(0f), 0f, "SmoothStep at 0");
            TestHarness.Near(Interpolation.SmoothStep(1f), 1f, "SmoothStep at 1");
            TestHarness.Near(Interpolation.SmoothStep(0.5f), 0.5f, "SmoothStep at 0.5");
            TestHarness.That(Interpolation.SmoothStep(0.25f) < 0.25f, "SmoothStep eases in");

            // Hermite spans start..end
            TestHarness.Near(Interpolation.Hermite(10f, 20f, 0f), 10f, "Hermite start");
            TestHarness.Near(Interpolation.Hermite(10f, 20f, 1f), 20f, "Hermite end");
            TestHarness.Near(Interpolation.Hermite(0f, 10f, 0.5f), 5f, "Hermite mid");

            // Sine easing endpoints and shape
            TestHarness.Near(Interpolation.EaseOutSine(0f), 0f, "EaseOutSine start");
            TestHarness.Near(Interpolation.EaseOutSine(1f), 1f, "EaseOutSine end");
            TestHarness.That(Interpolation.EaseOutSine(0.5f) > 0.5f, "EaseOutSine eases out");
            TestHarness.Near(Interpolation.EaseInSine(0f), 0f, "EaseInSine start");
            TestHarness.Near(Interpolation.EaseInSine(1f), 1f, "EaseInSine end");
            TestHarness.That(Interpolation.EaseInSine(0.5f) < 0.5f, "EaseInSine eases in");

            // Back ease-out: exact endpoints, overshoots above 1 near the end
            TestHarness.Near(Interpolation.EaseOutBack(0f), 0f, "EaseOutBack start");
            TestHarness.Near(Interpolation.EaseOutBack(1f), 1f, "EaseOutBack end");
            TestHarness.That(Interpolation.EaseOutBack(0.75f) > 1f, "EaseOutBack overshoots");

            // Bounce ease-out endpoints and range
            TestHarness.Near(Interpolation.EaseOutBounce(0f), 0f, "EaseOutBounce at 0");
            TestHarness.Near(Interpolation.EaseOutBounce(1f), 1f, "EaseOutBounce at 1", 1e-3);
            TestHarness.That(Interpolation.EaseOutBounce(0.5f) >= 0f && Interpolation.EaseOutBounce(0.5f) <= 1f,
                "EaseOutBounce mid in range");
        }
    }
}
