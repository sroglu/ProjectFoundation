using System;

namespace PFound.Utilities.MathTools.Tests
{
    /// <summary>
    /// Standalone entry point for the engine-free scalar core suites.
    /// Compile with: csc Core/*.cs Tests/*.cs and run under mono.
    /// (Harmless in Unity: never invoked, no UnityEngine dependency.)
    /// </summary>
    internal static class Program
    {
        public static int Main()
        {
            Console.WriteLine("PFound.Utilities.MathTools — scalar core characterization");
            Console.WriteLine(new string('=', 50));

            ScalarMathTests.Run();
            InterpolationTests.Run();
            StatisticsAndControlTests.Run();

            return TestHarness.Report();
        }
    }
}
