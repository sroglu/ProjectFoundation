using System;

namespace PFound.Utilities.MathTools.Tests
{
    internal static class StatisticsAndControlTests
    {
        public static void Run()
        {
            RunStatistics();
            RunPid();
        }

        private static void RunStatistics()
        {
            Console.WriteLine("RunningStatistics");

            var stats = new RunningStatistics();
            double[] samples = { 2, 4, 4, 4, 5, 5, 7, 9 }; // textbook set, popStdDev = 2, mean = 5
            foreach (double s in samples)
            {
                stats.Add(s);
            }

            TestHarness.That(stats.Count == 8, "Count tracks samples");
            TestHarness.Near(stats.Mean, 5d, "Welford mean");
            TestHarness.Near(stats.PopulationVariance, 4d, "Population variance");
            TestHarness.Near(stats.PopulationStandardDeviation, 2d, "Population std dev");
            TestHarness.Near(stats.SampleVariance, 32d / 7d, "Sample variance");

            stats.Reset();
            TestHarness.That(stats.Count == 0, "Reset clears count");
            TestHarness.Near(stats.Mean, 0d, "Reset clears mean");
        }

        private static void RunPid()
        {
            Console.WriteLine("PidController");

            // Pure proportional: output = Kp * error.
            var p = new PidController(2f, 0f, 0f);
            TestHarness.Near(p.Update(3f, 0.1f), 6f, "Proportional output");

            // Integral accumulates error * dt.
            var i = new PidController(0f, 1f, 0f);
            i.Update(1f, 1f);
            double second = i.Update(1f, 1f);
            TestHarness.Near(second, 2f, "Integral accumulation");

            // Integral clamp curbs wind-up.
            var clamped = new PidController(0f, 1f, 0f, 1.5f);
            clamped.Update(10f, 1f);
            TestHarness.Near(clamped.Integral, 1.5f, "Integral clamp");

            // Derivative reacts to change in error; first call has no derivative history.
            var d = new PidController(0f, 0f, 1f);
            double first = d.Update(5f, 1f);
            TestHarness.Near(first, 0f, "Derivative first call zero");
            double later = d.Update(8f, 1f); // delta 3 over dt 1
            TestHarness.Near(later, 3f, "Derivative on change");

            d.Reset();
            TestHarness.Near(d.Integral, 0f, "PID reset clears integral");
        }
    }
}
