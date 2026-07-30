namespace PFound.Render.PostProcess
{
    /// <summary>
    /// Stack-resolution policy declared by each <see cref="IRenderPostProcessAdapter{TRequest}"/>.
    /// </summary>
    public enum PostProcessBlendPolicy : byte
    {
        /// <summary>Walk the stack; highest-priority entry wins; ties broken by insertion order (later wins). Default for Blur + Outline.</summary>
        HighestPriorityWins = 0,

        /// <summary>Sum each entry's parameter value, fade-weighted, saturated at the parameter's documented max.</summary>
        WeightedSum = 1,

        /// <summary>Most recently added entry wins (regardless of priority) — the crossfade semantics used by LUT-style single-slot adapters.</summary>
        LatestWins = 2,
    }
}
