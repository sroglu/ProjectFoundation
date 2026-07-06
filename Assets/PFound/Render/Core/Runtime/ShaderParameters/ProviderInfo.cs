namespace PFound.Render.Core.ShaderParameters
{
    /// <summary>
    /// Debug snapshot of one registered provider, returned by
    /// <see cref="GlobalShaderParameterManager.GetSnapshot"/>.
    /// </summary>
    public struct ProviderInfo
    {
        public string DebugName;
        public int Priority;
        public int LastPublishedFrame;
    }
}