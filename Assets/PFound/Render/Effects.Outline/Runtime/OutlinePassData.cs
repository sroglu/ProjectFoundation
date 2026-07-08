using UnityEngine;

namespace PFound.Render.Effects.Outline
{
    /// <summary>
    /// Per-frame DTO populated by <c>OutlinePass.Populate</c> and read by <c>Execute</c>. URP RenderGraph
    /// pools instances of this class automatically — subclasses do not allocate per frame.
    /// </summary>
    public sealed class OutlinePassData
    {
        /// <summary>When false, Execute returns immediately (no shader binds, no RT leases).</summary>
        public bool Active;

        /// <summary>Resolved outline color. Alpha controls overlay opacity (premultiplied into the lerp weight).</summary>
        public Color EdgeColor;

        /// <summary>Effective sample offset radius in texels (1..16).</summary>
        public int Thickness;

        /// <summary>Composite mix factor in [0, 1] (0 = no outline, 1 = full edge composite).</summary>
        public float Strength;
    }
}
