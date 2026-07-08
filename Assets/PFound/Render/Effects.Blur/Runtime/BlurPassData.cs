namespace PFound.Render.Effects.Blur
{
    /// <summary>
    /// Per-frame DTO populated by <c>BlurPass.Populate</c> and read by <c>Execute</c>. URP RenderGraph
    /// pools instances of this class automatically — subclasses do not allocate per frame.
    /// </summary>
    public sealed class BlurPassData
    {
        /// <summary>When false, Execute returns immediately (no shader binds, no RT leases).</summary>
        public bool Active;

        /// <summary>Composite mix factor in [0, 1]. Only used in CameraComposite / Both modes.</summary>
        public float Strength;

        /// <summary>Effective downsample factor (1, 2, 4, or 8).</summary>
        public int Downsample;

        /// <summary>Effective iteration count (1..4).</summary>
        public int Iterations;

        /// <summary>Resolved per-frame output mode.</summary>
        public BlurOutputMode OutputMode;
    }
}