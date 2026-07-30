using System;

namespace PFound.TweenPresetLibrary.Core
{
    /// <summary>
    /// Evaluates an <see cref="Ease"/> curve at a normalized time. Pure, allocation-free, deterministic (only
    /// <see cref="System.Math"/>; no engine types). The Penner easing equations are public-domain math, reimplemented
    /// here from first principles in an original shape: each family is defined ONCE by its "In" curve, and the Out and
    /// InOut directions are derived by reflection
    /// (<c>Out(t) = 1 - In(1-t)</c>, <c>InOut(t) = t&lt;½ ? ½·In(2t) : 1 - ½·In(2-2t)</c>). Back/Elastic deliberately
    /// leave [0,1] near their ends; Bounce stays within it.
    /// </summary>
    public static class EaseEvaluator
    {
        /// <summary>The curve used for <see cref="Ease.Unset"/> and any out-of-range value.</summary>
        public const Ease DefaultEase = Ease.OutQuad;

        private const double HalfPi = Math.PI / 2.0;
        private const double TwoPi = Math.PI * 2.0;

        // Family indices in the order the Ease enum groups them (Sine, Quad, ... Bounce).
        private const int Sine = 0, Quad = 1, Cubic = 2, Quart = 3, Quint = 4,
                          Expo = 5, Circ = 6, Elastic = 7, Back = 8, Bounce = 9;
        private const int FamilyCount = 10;

        /// <summary>
        /// Curve value at normalized time <paramref name="t"/> (expected 0..1). <paramref name="overshoot"/> is Back's
        /// pull-back / Elastic's amplitude; <paramref name="period"/> is Elastic's period (≤0 ⇒ a sensible default).
        /// </summary>
        public static float Evaluate(Ease ease, float t, float overshoot = 1.70158f, float period = 0f)
        {
            if (ease == Ease.Linear) return t;

            int index = (int)ease - (int)Ease.InSine;
            if (index < 0 || index >= FamilyCount * 3)            // Unset or out of range → the default curve.
                index = (int)DefaultEase - (int)Ease.InSine;

            int family = index / 3;
            int direction = index % 3;                            // 0 = In, 1 = Out, 2 = InOut

            switch (direction)
            {
                case 1: // Out: mirror the In curve about both axes.
                    return 1f - In(family, 1f - t, overshoot, period);
                case 2: // InOut: In curve on the first half, its mirror on the second.
                    return t < 0.5f
                        ? 0.5f * In(family, 2f * t, overshoot, period)
                        : 1f - 0.5f * In(family, 2f - 2f * t, overshoot, period);
                default: // In
                    return In(family, t, overshoot, period);
            }
        }

        /// <summary>The "In" form of each family at <paramref name="t"/> — the single source every direction derives from.</summary>
        private static float In(int family, float t, float overshoot, float period)
        {
            double d = t;
            switch (family)
            {
                case Sine: return (float)(1.0 - Math.Cos(d * HalfPi));
                case Quad: return (float)(d * d);
                case Cubic: return (float)(d * d * d);
                case Quart: return (float)(d * d * d * d);
                case Quint: return (float)(d * d * d * d * d);
                case Expo: return t <= 0f ? 0f : (float)Math.Pow(2.0, 10.0 * (d - 1.0));
                case Circ: return (float)(1.0 - Math.Sqrt(Math.Max(0.0, 1.0 - d * d)));
                case Elastic: return InElastic(t, overshoot, period);
                case Back: return InBack(t, overshoot);
                case Bounce: return 1f - OutBounce(1f - t);
                default: return t;
            }
        }

        private static float InBack(float t, float overshoot)
        {
            double s = overshoot;
            double d = t;
            return (float)(d * d * ((s + 1.0) * d - s)); // dips below 0 before climbing to 1
        }

        private static float InElastic(float t, float amplitude, float period)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;

            double p = period > 0f ? period : 0.3;
            double a = amplitude;
            double s;
            if (a < 1.0) { a = 1.0; s = p * 0.25; }                 // tiny amplitude → quarter-period offset
            else { s = p / TwoPi * Math.Asin(1.0 / a); }            // phase that lands In(1)=1

            double td = t - 1.0;
            return (float)(-(a * Math.Pow(2.0, 10.0 * td) * Math.Sin((td - s) * TwoPi / p)));
        }

        private static float OutBounce(float t)
        {
            // Four parabolic arcs of shrinking height — the classic damped bounce, all within [0,1].
            const double n = 7.5625, d = 2.75;
            double x = t;
            if (x < 1.0 / d) return (float)(n * x * x);
            if (x < 2.0 / d) { x -= 1.5 / d; return (float)(n * x * x + 0.75); }
            if (x < 2.5 / d) { x -= 2.25 / d; return (float)(n * x * x + 0.9375); }
            x -= 2.625 / d;
            return (float)(n * x * x + 0.984375);
        }
    }
}
