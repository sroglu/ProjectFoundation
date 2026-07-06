using System;
using PFound.TweenPresetLibrary.Core;

namespace PFound.TweenPresetLibrary.Core.Tests
{
    /// <summary>
    /// Standalone mono/csc runner for the engine-free easing core — asserts the curve contract against the math
    /// directly (endpoints, Linear identity, default fallback, mid-points, overshoot behavior). No Unity.
    /// </summary>
    internal static class Program
    {
        private const float Eps = 1e-4f;
        private const int First = (int)Ease.InSine;
        private const int Last = (int)Ease.InOutBounce;

        private static int s_passed;
        private static int s_failed;

        private static int Main()
        {
            Run("Linear is the identity", Linear_Identity);
            Run("Unset resolves to OutQuad", Unset_IsOutQuad);
            Run("Out-of-range resolves to OutQuad", OutOfRange_IsOutQuad);
            Run("DefaultEase is OutQuad", () => AssertEqual((int)Ease.OutQuad, (int)EaseEvaluator.DefaultEase, "DefaultEase"));
            Run("Every curve maps t=0 → 0 and t=1 → 1", AllCurves_HitEndpoints);
            Run("Every InOut curve passes through 0.5 at t=0.5", AllInOut_MidpointIsHalf);
            Run("Polynomial mid-points match the math", Polynomials_MidPoints);
            Run("Sine/Circ/Expo mid-points match the math", Transcendental_MidPoints);
            Run("Back exits [0,1] (under/overshoot)", Back_Overshoots);
            Run("Elastic exits [0,1]", Elastic_Overshoots);
            Run("Bounce stays within [0,1]", Bounce_InRange);
            Run("Out is the reflection of In", Out_ReflectsIn);

            Console.WriteLine();
            Console.WriteLine(s_failed == 0 ? "ALL PASSED (" + s_passed + ")" : (s_passed + " passed, " + s_failed + " FAILED"));
            return s_failed == 0 ? 0 : 1;
        }

        private static void Linear_Identity()
        {
            foreach (float t in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
                AssertClose(t, EaseEvaluator.Evaluate(Ease.Linear, t), "Linear(" + t + ")");
        }

        private static void Unset_IsOutQuad()
        {
            for (float t = 0f; t <= 1f; t += 0.1f)
                AssertClose(EaseEvaluator.Evaluate(Ease.OutQuad, t), EaseEvaluator.Evaluate(Ease.Unset, t), "Unset==OutQuad @" + t);
        }

        private static void OutOfRange_IsOutQuad()
        {
            AssertClose(EaseEvaluator.Evaluate(Ease.OutQuad, 0.5f), EaseEvaluator.Evaluate((Ease)999, 0.5f), "OOB high");
            AssertClose(EaseEvaluator.Evaluate(Ease.OutQuad, 0.5f), EaseEvaluator.Evaluate((Ease)(-7), 0.5f), "OOB low");
        }

        private static void AllCurves_HitEndpoints()
        {
            for (int e = First; e <= Last; e++)
            {
                var ease = (Ease)e;
                AssertClose(0f, EaseEvaluator.Evaluate(ease, 0f), ease + "(0)");
                AssertClose(1f, EaseEvaluator.Evaluate(ease, 1f), ease + "(1)");
            }
        }

        private static void AllInOut_MidpointIsHalf()
        {
            for (int e = First; e <= Last; e++)
                if ((e - First) % 3 == 2) // InOut direction
                    AssertClose(0.5f, EaseEvaluator.Evaluate((Ease)e, 0.5f), (Ease)e + "(0.5)");
        }

        private static void Polynomials_MidPoints()
        {
            AssertClose(0.25f, EaseEvaluator.Evaluate(Ease.InQuad, 0.5f), "InQuad(0.5)");
            AssertClose(0.75f, EaseEvaluator.Evaluate(Ease.OutQuad, 0.5f), "OutQuad(0.5)");
            AssertClose(0.125f, EaseEvaluator.Evaluate(Ease.InCubic, 0.5f), "InCubic(0.5)");
            AssertClose(0.875f, EaseEvaluator.Evaluate(Ease.OutCubic, 0.5f), "OutCubic(0.5)");
            AssertClose(0.0625f, EaseEvaluator.Evaluate(Ease.InQuart, 0.5f), "InQuart(0.5)");
            AssertClose(1f / 32f, EaseEvaluator.Evaluate(Ease.InQuint, 0.5f), "InQuint(0.5)");
        }

        private static void Transcendental_MidPoints()
        {
            AssertClose(1f - (float)Math.Cos(0.5 * Math.PI / 2), EaseEvaluator.Evaluate(Ease.InSine, 0.5f), "InSine(0.5)");
            AssertClose(1f - (float)Math.Sqrt(1 - 0.25), EaseEvaluator.Evaluate(Ease.InCirc, 0.5f), "InCirc(0.5)");
            AssertClose((float)Math.Pow(2, 10 * (0.5 - 1)), EaseEvaluator.Evaluate(Ease.InExpo, 0.5f), "InExpo(0.5)");
        }

        private static void Back_Overshoots()
        {
            Assert(EaseEvaluator.Evaluate(Ease.InBack, 0.2f) < 0f, "InBack dips below 0");
            Assert(EaseEvaluator.Evaluate(Ease.OutBack, 0.7f) > 1f, "OutBack rises above 1");
        }

        private static void Elastic_Overshoots()
        {
            bool outOfRange = false;
            for (float t = 0.05f; t < 1f; t += 0.05f)
            {
                float v = EaseEvaluator.Evaluate(Ease.OutElastic, t);
                if (v < 0f || v > 1f) { outOfRange = true; break; }
            }
            Assert(outOfRange, "OutElastic leaves [0,1] somewhere");
        }

        private static void Bounce_InRange()
        {
            for (int e = (int)Ease.InBounce; e <= (int)Ease.InOutBounce; e++)
                for (float t = 0f; t <= 1f; t += 0.05f)
                {
                    float v = EaseEvaluator.Evaluate((Ease)e, t);
                    Assert(v >= -Eps && v <= 1f + Eps, (Ease)e + "(" + t + ")=" + v + " in [0,1]");
                }
        }

        private static void Out_ReflectsIn()
        {
            // Out(t) must equal 1 - In(1-t) for a non-overshoot family.
            foreach (float t in new[] { 0.2f, 0.5f, 0.8f })
            {
                float outV = EaseEvaluator.Evaluate(Ease.OutCubic, t);
                float reflected = 1f - EaseEvaluator.Evaluate(Ease.InCubic, 1f - t);
                AssertClose(reflected, outV, "OutCubic reflects InCubic @" + t);
            }
        }

        // ---- harness ----
        private static void Run(string name, Action test)
        {
            try { test(); s_passed++; Console.WriteLine("  PASS  " + name); }
            catch (Exception e) { s_failed++; Console.WriteLine("  FAIL  " + name + " :: " + e.Message); }
        }

        private static void Assert(bool condition, string what)
        {
            if (!condition) throw new Exception("assertion failed: " + what);
        }

        private static void AssertClose(float expected, float actual, string what)
        {
            if (Math.Abs(expected - actual) > Eps)
                throw new Exception("expected " + expected + " but got " + actual + " for " + what);
        }

        private static void AssertEqual(int expected, int actual, string what)
        {
            if (expected != actual) throw new Exception("expected " + expected + " but got " + actual + " for " + what);
        }
    }
}
