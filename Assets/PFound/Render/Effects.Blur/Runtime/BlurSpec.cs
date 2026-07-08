namespace PFound.Render.Effects.Blur
{
    /// <summary>
    /// Immutable per-request payload for <see cref="IBlurRequestService"/>. Strength is the
    /// composite mix factor in [0, 1]; OutputMode selects camera composite / global texture
    /// publish / both. Downsample and Iterations are optional per-request overrides — when
    /// null the volume's default quality settings apply.
    /// </summary>
    /// <remarks>
    /// Named <c>BlurSpec</c> (not <c>BlurRequest</c>) to avoid collision with the
    /// <c>PFound.Render.PostProcess.BlurRequest</c> struct used by Phase 6's adapter pattern.
    /// A future PostProcess unification may promote this to <c>BlurRequest</c> and remove the
    /// PostProcess-side duplicate.
    /// </remarks>
    public readonly struct BlurSpec
    {
        public readonly float Strength;
        public readonly BlurOutputMode OutputMode;
        public readonly BlurDownsample? Downsample;
        public readonly int? Iterations;

        public BlurSpec(
            float strength,
            BlurOutputMode outputMode = BlurOutputMode.CameraComposite,
            BlurDownsample? downsample = null,
            int? iterations = null)
        {
            Strength = strength;
            OutputMode = outputMode;
            Downsample = downsample;
            Iterations = iterations;
        }
    }
}
