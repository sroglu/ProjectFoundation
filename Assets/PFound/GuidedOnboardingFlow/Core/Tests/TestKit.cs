using System;

namespace PFound.GuidedOnboardingFlow.Core.Tests
{
    /// <summary>Tiny shared assertion harness for the standalone mono/csc runner — no NUnit, no Unity.</summary>
    internal static class TestKit
    {
        private static int s_passed;
        private static int s_failed;

        public static void Run(string name, Action body)
        {
            try
            {
                body();
                s_passed++;
                Console.WriteLine("  ok   " + name);
            }
            catch (Exception e)
            {
                s_failed++;
                Console.WriteLine("  FAIL " + name + " -> " + e.Message);
            }
        }

        public static void IsTrue(bool condition, string label)
        {
            if (!condition)
                throw new Exception("expected true: " + label);
        }

        public static void IsFalse(bool condition, string label) => IsTrue(!condition, label);

        public static void AreEqual<T>(T expected, T actual, string label)
        {
            if (!Equals(expected, actual))
                throw new Exception(label + " expected <" + expected + "> but was <" + actual + ">");
        }

        public static int Summary(string module)
        {
            Console.WriteLine();
            Console.WriteLine(s_failed == 0
                ? module + ": ALL PASSED (" + s_passed + ")"
                : module + ": " + s_passed + " passed, " + s_failed + " FAILED");
            return s_failed == 0 ? 0 : 1;
        }
    }
}
