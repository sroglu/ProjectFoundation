using UnityEngine.Rendering.RenderGraphModule;

namespace PFound.Render.Effects.Overdraw
{
    /// <summary>
    /// Per-frame DTO populated by <c>OverdrawPass.Populate</c> and read by <c>Execute</c>. URP RenderGraph
    /// pools instances of this class automatically — subclasses do not allocate per frame.
    /// </summary>
    public sealed class OverdrawPassData
    {
        /// <summary>When false, Execute returns immediately (no draw calls, no shader binds).</summary>
        public bool Active;

        /// <summary>Constant value the fragment shader emits per fragment (additive accumulator step).</summary>
        public float ContributionScalar;

        /// <summary>Number of active tier pairs in the threshold ramp (1..OverdrawPass.MaxThresholds).</summary>
        public int ThresholdCount;

        /// <summary>Transient accumulator RT leased from the pool for this frame.</summary>
        public TextureHandle AccumulatorRT;

        /// <summary>URP renderer list for the opaque queue (override-material draw into accumulator).</summary>
        public RendererListHandle OpaqueRendererList;

        /// <summary>URP renderer list for the transparent queue (override-material draw into accumulator).</summary>
        public RendererListHandle TransparentRendererList;
    }
}
