namespace PFound.Render.PostProcess
{
    /// <summary>
    /// Configuration snapshot consumed by <see cref="RenderPostProcessService"/>. Per-adapter
    /// blend-policy overrides + global caps.
    /// </summary>
    public sealed class RenderPostProcessOptions
    {
        /// <summary>Default policy for the built-in Blur adapter.</summary>
        public PostProcessBlendPolicy BlurPolicy { get; set; } = PostProcessBlendPolicy.HighestPriorityWins;

        /// <summary>Default policy for the built-in Outline adapter.</summary>
        public PostProcessBlendPolicy OutlinePolicy { get; set; } = PostProcessBlendPolicy.HighestPriorityWins;

        /// <summary>Defensive cap per effect type. Requests beyond this are silently dropped + one-shot warned.</summary>
        public int MaxConcurrentRequestsPerEffect { get; set; } = 256;

        /// <summary>When true, an adapter whose underlying VolumeComponent is missing emits one Debug.LogWarning per session.</summary>
        public bool WarnOnMissingVolumeComponent { get; set; } = true;

        /// <summary>Returns a fresh options instance carrying the spec-mandated defaults.</summary>
        public static RenderPostProcessOptions Default => new RenderPostProcessOptions();
    }
}
