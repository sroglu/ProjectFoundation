namespace PFound.Render.Core.ShaderParameters
{
    /// <summary>
    /// Implemented by client code that wants to publish per-frame shader globals
    /// through <see cref="GlobalShaderParameterManager"/>. Providers are invoked
    /// once per frame, in priority order (ascending; FIFO tiebreaker).
    /// </summary>
    public interface IGlobalShaderParameterProvider
    {
        /// <summary>Stable debug name for the provider; surfaced by the editor enumeration API.</summary>
        string DebugName { get; }

        /// <summary>Called once per frame, in priority order. Implementation calls <c>Shader.SetGlobal*</c> directly.</summary>
        void Publish();
    }
}