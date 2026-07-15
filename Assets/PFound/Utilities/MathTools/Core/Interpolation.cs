using System;

namespace PFound.Utilities.MathTools
{
    /// <summary>
    /// Engine-free interpolation and easing curves. Easing helpers take a normalized
    /// progress <c>t</c> in 0..1 and return the shaped progress (also normalized, apart
    /// from the deliberate overshoot in <see cref="EaseOutBack"/>). The <see cref="Hermite"/>
    /// helper blends between an explicit <c>start</c> and <c>end</c>.
    /// </summary>
    public static class Interpolation
    {
        private const float HalfPi = (float)(Math.PI * 0.5);

        // Overshoot amount for the back curve. 1.70158 is the classic constant that
        // yields roughly 10% overshoot past the target before settling.
        private const float BackOvershoot = 1.70158f;

        /// <summary>Classic cubic smoothstep of a normalized progress: 3t^2 - 2t^3.</summary>
        public static float SmoothStep(float t)
        {
            t = ScalarMath.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Smoothstep-shaped interpolation between <paramref name="start"/> and <paramref name="end"/>.</summary>
        public static float Hermite(float start, float end, float t)
        {
            return start + (end - start) * SmoothStep(t);
        }

        /// <summary>Sine ease-in: gentle start, accelerating finish. 1 - cos(t*PI/2).</summary>
        public static float EaseInSine(float t)
        {
            t = ScalarMath.Clamp01(t);
            return 1f - (float)Math.Cos(t * HalfPi);
        }

        /// <summary>Sine ease-out: fast start, gentle finish. sin(t*PI/2).</summary>
        public static float EaseOutSine(float t)
        {
            t = ScalarMath.Clamp01(t);
            return (float)Math.Sin(t * HalfPi);
        }

        /// <summary>
        /// Back ease-out: overshoots the target near the end and settles back to 1
        /// (a soft "boing"). Standard cubic back form built around <see cref="BackOvershoot"/>.
        /// </summary>
        public static float EaseOutBack(float t)
        {
            t = ScalarMath.Clamp01(t);
            float u = t - 1f;
            return 1f + (BackOvershoot + 1f) * u * u * u + BackOvershoot * u * u;
        }

        /// <summary>
        /// Bounce ease-out: the target is approached with a series of decaying bounces.
        /// Standard four-segment quadratic bounce (amplitude 7.5625 over a 2.75 timeline).
        /// </summary>
        public static float EaseOutBounce(float t)
        {
            const float amplitude = 7.5625f;
            const float timeline = 2.75f;
            t = ScalarMath.Clamp01(t);

            if (t < 1f / timeline)
            {
                return amplitude * t * t;
            }
            if (t < 2f / timeline)
            {
                t -= 1.5f / timeline;
                return amplitude * t * t + 0.75f;
            }
            if (t < 2.5f / timeline)
            {
                t -= 2.25f / timeline;
                return amplitude * t * t + 0.9375f;
            }
            t -= 2.625f / timeline;
            return amplitude * t * t + 0.984375f;
        }
    }
}
