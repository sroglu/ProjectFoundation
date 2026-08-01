using System;

namespace PFound.Backend.Core
{
    public static class TestKit
    {
        public static int Passed;
        public static int Failed;

        public static void Check(bool condition, string message)
        {
            if (condition)
                Passed++;
            else
            {
                Failed++;
                Console.WriteLine("  FAIL: " + message);
            }
        }

        public static void Throws<T>(Action action, string message) where T : Exception
        {
            try
            {
                action();
                Failed++;
                Console.WriteLine("  FAIL (no throw): " + message);
            }
            catch (T)
            {
                Passed++;
            }
            catch (Exception e)
            {
                Failed++;
                Console.WriteLine("  FAIL (wrong ex " + e.GetType().Name + "): " + message);
            }
        }

        public static int Summary(string label)
        {
            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine(label + ": passed=" + Passed + " failed=" + Failed);
            return Failed == 0 ? 0 : 1;
        }
    }
}
