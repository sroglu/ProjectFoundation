using System;

namespace PFound.Utilities.MathTools
{
    /// <summary>
    /// Engine-free scalar helpers: range remapping, clamping, wrapping, tolerance
    /// comparisons, rounding/snapping, sign-preserving powers and misc number utilities.
    /// Implemented on top of <see cref="System.Math"/> so the assembly stays UnityEngine-free
    /// and runs under a plain mono/csc test runner.
    /// </summary>
    public static class ScalarMath
    {
        public const float Tau = (float)(Math.PI * 2.0);
        public const float Pi = (float)Math.PI;
        public const float DefaultTolerance = 1e-5f;

        // ---- Range remapping ----------------------------------------------------

        /// <summary>Linearly maps <paramref name="value"/> from [inMin,inMax] onto [outMin,outMax].</summary>
        public static float Remap(float value, float inMin, float inMax, float outMin, float outMax)
        {
            return outMin + (value - inMin) * (outMax - outMin) / (inMax - inMin);
        }

        /// <summary>As <see cref="Remap"/> but the result is clamped to the output range (handles inverted ranges).</summary>
        public static float RemapClamped(float value, float inMin, float inMax, float outMin, float outMax)
        {
            float mapped = Remap(value, inMin, inMax, outMin, outMax);
            float lo = Math.Min(outMin, outMax);
            float hi = Math.Max(outMin, outMax);
            return Math.Min(hi, Math.Max(lo, mapped));
        }

        // ---- Clamping -----------------------------------------------------------

        /// <summary>Clamps to the unit interval [0,1].</summary>
        public static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        /// <summary>Clamps to the signed unit interval [-1,1].</summary>
        public static float ClampSigned(float value)
        {
            if (value < -1f) return -1f;
            if (value > 1f) return 1f;
            return value;
        }

        // ---- Wrapping -----------------------------------------------------------

        /// <summary>Wraps a value into the half-open unit interval [0,1).</summary>
        public static float Wrap01(float value)
        {
            return Wrap(value, 0f, 1f);
        }

        /// <summary>Wraps a value into the half-open interval [min,max).</summary>
        public static float Wrap(float value, float min, float max)
        {
            float span = max - min;
            float offset = value - min;
            return min + (offset - (float)Math.Floor(offset / span) * span);
        }

        /// <summary>Wraps an angle in radians into the symmetric range [-PI,PI).</summary>
        public static float WrapRadiansSigned(float radians)
        {
            return Wrap(radians, -Pi, Pi);
        }

        /// <summary>Wraps an angle in radians into the positive range [0,2PI).</summary>
        public static float WrapRadiansPositive(float radians)
        {
            return Wrap(radians, 0f, Tau);
        }

        // ---- Tolerance comparisons ---------------------------------------------

        /// <summary>True when the magnitude is within <paramref name="tolerance"/> of zero.</summary>
        public static bool IsNearZero(float value, float tolerance = DefaultTolerance)
        {
            return Math.Abs(value) <= tolerance;
        }

        /// <summary>True when two values differ by no more than <paramref name="tolerance"/>.</summary>
        public static bool Approximately(float a, float b, float tolerance = DefaultTolerance)
        {
            return Math.Abs(a - b) <= tolerance;
        }

        /// <summary>Inclusive range test: min &lt;= value &lt;= max.</summary>
        public static bool IsBetween(float value, float min, float max)
        {
            return value >= min && value <= max;
        }

        /// <summary>Exclusive range test: min &lt; value &lt; max.</summary>
        public static bool IsBetweenExclusive(float value, float min, float max)
        {
            return value > min && value < max;
        }

        // ---- Rounding & snapping ------------------------------------------------

        /// <summary>Rounds to <paramref name="decimals"/> fractional digits (away-from-zero midpoints).</summary>
        public static float RoundToDecimals(float value, int decimals)
        {
            return (float)Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        }

        /// <summary>Snaps to the nearest multiple of <paramref name="step"/>.</summary>
        public static float SnapToStep(float value, float step)
        {
            if (step == 0f) return value;
            return (float)Math.Round(value / step, MidpointRounding.AwayFromZero) * step;
        }

        /// <summary>True when the value already lies on a multiple of <paramref name="step"/>.</summary>
        public static bool IsSnapped(float value, float step, float tolerance = DefaultTolerance)
        {
            return IsNearZero(value - SnapToStep(value, step), tolerance);
        }

        // ---- Sign-preserving powers --------------------------------------------

        /// <summary>Square root of the magnitude, carrying the original sign.</summary>
        public static float SignedSqrt(float value)
        {
            return Math.Sign(value) * (float)Math.Sqrt(Math.Abs(value));
        }

        /// <summary>Squares the magnitude while preserving the sign (value * |value|).</summary>
        public static float SignedSquare(float value)
        {
            return value * Math.Abs(value);
        }

        /// <summary>Raises the magnitude to <paramref name="exponent"/>, carrying the original sign.</summary>
        public static float SignedPow(float value, float exponent)
        {
            return Math.Sign(value) * (float)Math.Pow(Math.Abs(value), exponent);
        }

        // ---- Misc number utilities ---------------------------------------------

        /// <summary>Number of decimal digits in the absolute value (zero counts as one digit).</summary>
        public static int DigitCount(long value)
        {
            long magnitude = value < 0 ? -value : value;
            int digits = 1;
            while (magnitude >= 10)
            {
                magnitude /= 10;
                digits++;
            }
            return digits;
        }

        /// <summary>True when <paramref name="value"/> is a positive power of two.</summary>
        public static bool IsPowerOfTwo(long value)
        {
            return value > 0 && (value & (value - 1)) == 0;
        }

        /// <summary>Returns zero when the input is NaN, otherwise the input unchanged.</summary>
        public static float ZeroIfNaN(float value)
        {
            return float.IsNaN(value) ? 0f : value;
        }

        // ---- Time unit conversions ---------------------------------------------

        public static float SecondsToMilliseconds(float seconds)
        {
            return seconds * 1000f;
        }

        public static float MillisecondsToSeconds(float milliseconds)
        {
            return milliseconds / 1000f;
        }
    }
}
