using System;

namespace PFound.Utilities.MathTools
{
    /// <summary>
    /// Incremental mean and variance accumulator using Welford's online algorithm.
    /// Feed samples one at a time via <see cref="Add"/>; mean, variance and standard
    /// deviation stay available at O(1) per sample without retaining the sample history.
    /// </summary>
    public struct RunningStatistics
    {
        private long _count;
        private double _mean;
        private double _sumOfSquaredDeltas; // running sum of (x - mean)^2 (Welford's M2)

        public long Count => _count;

        public double Mean => _count > 0 ? _mean : 0d;

        /// <summary>Population variance (divides by N). Zero when fewer than two samples.</summary>
        public double PopulationVariance => _count > 0 ? _sumOfSquaredDeltas / _count : 0d;

        /// <summary>Sample variance (divides by N-1). Zero when fewer than two samples.</summary>
        public double SampleVariance => _count > 1 ? _sumOfSquaredDeltas / (_count - 1) : 0d;

        public double PopulationStandardDeviation => Math.Sqrt(PopulationVariance);

        public double SampleStandardDeviation => Math.Sqrt(SampleVariance);

        /// <summary>Incorporates a new sample into the running statistics.</summary>
        public void Add(double sample)
        {
            _count++;
            double deltaBefore = sample - _mean;
            _mean += deltaBefore / _count;
            double deltaAfter = sample - _mean;
            _sumOfSquaredDeltas += deltaBefore * deltaAfter;
        }

        /// <summary>Resets the accumulator to its empty state.</summary>
        public void Reset()
        {
            _count = 0;
            _mean = 0d;
            _sumOfSquaredDeltas = 0d;
        }
    }
}
