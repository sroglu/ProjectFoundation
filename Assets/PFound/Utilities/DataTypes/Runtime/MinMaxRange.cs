using System;
using System.Globalization;

namespace PFound.Utilities.DataType
{
    /// <summary>
    /// An inclusive floating-point interval [Min, Max]. Engine-free: randomization takes an explicit
    /// <see cref="System.Random"/> rather than depending on UnityEngine.Random.
    /// </summary>
    [Serializable]
    public struct MinMaxRange : IEquatable<MinMaxRange>
    {
        public float Min;
        public float Max;

        public MinMaxRange(float min, float max)
        {
            Min = min;
            Max = max;
        }

        /// <summary>An "unset" range whose bounds are inverted, ready to be grown via Encapsulate.</summary>
        public static MinMaxRange Invalid => new MinMaxRange(float.MaxValue, float.MinValue);

        /// <summary>The degenerate range [0, 0].</summary>
        public static MinMaxRange Zero => new MinMaxRange(0f, 0f);

        /// <summary>The unit range [0, 1].</summary>
        public static MinMaxRange Identity => new MinMaxRange(0f, 1f);

        /// <summary>True when the bounds are ordered (Min &lt;= Max).</summary>
        public bool IsValid => Min <= Max;

        /// <summary>The width of the interval (Max - Min).</summary>
        public float Length => Max - Min;

        /// <summary>The midpoint of the interval.</summary>
        public float Center => (Min + Max) * 0.5f;

        /// <summary>Clamps a value into [Min, Max].</summary>
        public float Clamp(float value)
        {
            if (value < Min) return Min;
            if (value > Max) return Max;
            return value;
        }

        /// <summary>True when <paramref name="value"/> lies within the inclusive interval.</summary>
        public bool Contains(float value) => value >= Min && value <= Max;

        /// <summary>Linearly interpolates from Min to Max. <paramref name="t"/> is clamped to [0, 1].</summary>
        public float Lerp(float t)
        {
            if (t <= 0f) return Min;
            if (t >= 1f) return Max;
            return Min + (Max - Min) * t;
        }

        /// <summary>The inverse of <see cref="Lerp"/>: maps a value to its [0, 1] position in the range.</summary>
        public float InverseLerp(float value)
        {
            var length = Max - Min;
            if (Math.Abs(length) < float.Epsilon) return 0f;
            var t = (value - Min) / length;
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return t;
        }

        /// <summary>Grows the range so it includes <paramref name="value"/>.</summary>
        public void Encapsulate(float value)
        {
            if (value < Min) Min = value;
            if (value > Max) Max = value;
        }

        /// <summary>Returns a uniformly distributed value inside the range using the supplied generator.</summary>
        public float Random(Random random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            return Min + (float)random.NextDouble() * (Max - Min);
        }

        public bool Equals(MinMaxRange other) =>
            Min.Equals(other.Min) && Max.Equals(other.Max);

        public override bool Equals(object obj) => obj is MinMaxRange other && Equals(other);

        public override int GetHashCode() => (Min.GetHashCode() * 397) ^ Max.GetHashCode();

        public static bool operator ==(MinMaxRange a, MinMaxRange b) => a.Equals(b);

        public static bool operator !=(MinMaxRange a, MinMaxRange b) => !a.Equals(b);

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "[{0}, {1}]", Min, Max);
    }
}
