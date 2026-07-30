namespace PFound.Render.PostProcess
{
    /// <summary>
    /// Request payload for the built-in <c>BlurAdapter</c>. Drives Phase 3's
    /// <c>BlurStrengthVolumeComponent.Strength</c>.
    /// </summary>
    public readonly struct BlurRequest
    {
        /// <summary>Target Strength in [0, 1].</summary>
        public readonly float Strength;

        /// <summary>Seconds to ramp from baseline to target on Request. 0 = instant.</summary>
        public readonly float FadeIn;

        /// <summary>Seconds to ramp from target to baseline on Release. 0 = mirror <see cref="FadeIn"/>.</summary>
        public readonly float FadeOut;

        /// <summary>Constructs a Blur request.</summary>
        public BlurRequest(float strength, float fadeIn = 0f, float fadeOut = 0f)
        {
            Strength = strength;
            FadeIn = fadeIn;
            FadeOut = fadeOut;
        }
    }
}
