using UnityEngine;

namespace PFound.Render.PostProcess
{
    /// <summary>
    /// Request payload for the built-in <c>OutlineAdapter</c>. Drives Phase 4's
    /// <c>OutlineVolumeComponent.{Strength, EdgeColor, Thickness}</c> in one coherent push.
    /// </summary>
    public readonly struct OutlineRequest
    {
        /// <summary>Target Strength in [0, 1].</summary>
        public readonly float Strength;

        /// <summary>Target outline color (alpha honored).</summary>
        public readonly Color EdgeColor;

        /// <summary>Target Thickness in [1, 16].</summary>
        public readonly int Thickness;

        /// <summary>Seconds to ramp from baseline to target on Request. 0 = instant.</summary>
        public readonly float FadeIn;

        /// <summary>Seconds to ramp from target to baseline on Release. 0 = mirror <see cref="FadeIn"/>.</summary>
        public readonly float FadeOut;

        /// <summary>Constructs an Outline request.</summary>
        public OutlineRequest(float strength, Color edgeColor, int thickness, float fadeIn = 0f, float fadeOut = 0f)
        {
            Strength = strength;
            EdgeColor = edgeColor;
            Thickness = thickness;
            FadeIn = fadeIn;
            FadeOut = fadeOut;
        }
    }
}
