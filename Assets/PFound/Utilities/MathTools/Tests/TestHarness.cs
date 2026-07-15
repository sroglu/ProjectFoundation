using System;

namespace PFound.Utilities.MathTools.Tests
{
    /// <summary>
    /// Minimal NUnit-free assertion harness so the engine-free scalar core can be
    /// exercised under a plain mono/csc runner without pulling in UnityEngine.
    /// </summary>
    internal static class TestHarness
    {
        public static int Ran;
        public static int Failed;

        public static void That(bool condition, string label)
        {
            Ran++;
            if (!condition)
            {
                Failed++;
                Console.WriteLine("  [x] " + label);
            }
        }

        public static void Near(double actual, double expected, string label, double tolerance = 1e-4)
        {
            That(Math.Abs(actual - expected) <= tolerance,
                label + " (expected " + expected + ", got " + actual + ")");
        }

        public static int Report()
        {
            Console.WriteLine(new string('-', 50));
            Console.WriteLine("MathTools scalar core: " + (Ran - Failed) + "/" + Ran + " passed.");
            return Failed == 0 ? 0 : 1;
        }
    }
}
