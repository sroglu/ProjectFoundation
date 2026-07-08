using UnityEngine;

namespace PFound.Render.Effects.Outline
{
    /// <summary>
    /// Immutable per-request payload for <see cref="IOutlineRequestService"/>.
    /// Strength is the composite mix factor in [0, 1]; EdgeColor's alpha controls overlay
    /// opacity; Thickness is the sample offset radius in texels [1, 16].
    /// </summary>
    /// <remarks>
    /// Named <c>OutlineSpec</c> (not <c>OutlineRequest</c>) to avoid collision with the
    /// <c>PFound.Render.PostProcess.OutlineRequest</c> struct used by Phase 6's adapter.
    /// </remarks>
    public readonly struct OutlineSpec
    {
        public readonly float Strength;
        public readonly Color EdgeColor;
        public readonly int Thickness;

        public OutlineSpec(float strength, Color edgeColor, int thickness)
        {
            Strength = strength;
            EdgeColor = edgeColor;
            Thickness = thickness;
        }
    }
}
