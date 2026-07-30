using System;

namespace PFound.InputRouter.Tests
{
    /// <summary>
    /// Dependency-free assertion tally so the engine-free core can be exercised under mono/csc
    /// with no NUnit and no Unity. Exit code 0 means every check was green.
    /// </summary>
    internal static class Assert
    {
        public static int Passed;
        public static int Failed;

        public static void That(bool condition, string label)
        {
            if (condition)
            {
                Passed++;
            }
            else
            {
                Failed++;
                Console.WriteLine("  FAIL: " + label);
            }
        }

        public static void Equal<T>(T expected, T actual, string label)
        {
            bool ok = Equals(expected, actual);
            if (ok)
            {
                Passed++;
            }
            else
            {
                Failed++;
                Console.WriteLine("  FAIL: " + label + " (expected=" + expected + " actual=" + actual + ")");
            }
        }

        public static void Rejects<TException>(Action action, string label) where TException : Exception
        {
            try
            {
                action();
                Failed++;
                Console.WriteLine("  FAIL (expected throw): " + label);
            }
            catch (TException)
            {
                Passed++;
            }
            catch (Exception e)
            {
                Failed++;
                Console.WriteLine("  FAIL (wrong exception " + e.GetType().Name + "): " + label);
            }
        }

        public static int Report(string suite)
        {
            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine(suite + ": passed=" + Passed + " failed=" + Failed);
            return Failed == 0 ? 0 : 1;
        }
    }
}
