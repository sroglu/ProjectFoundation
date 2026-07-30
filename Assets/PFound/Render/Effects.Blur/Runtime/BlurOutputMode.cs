namespace PFound.Render.Effects.Blur
{
    /// <summary>
    /// Output dispatch mode for <c>BlurPass</c>. Controls whether the blurred result is composited
    /// into the camera color attachment, published as the <c>_RenderBlurTexture</c> global, or both.
    /// </summary>
    public enum BlurOutputMode
    {
        /// <summary>Override camera color attachment via <c>lerp(original, blurred, Strength)</c>. Strength acts as the mix factor.</summary>
        CameraComposite,

        /// <summary>Publish blurred result as the <c>_RenderBlurTexture</c> Shader global. Camera output unchanged. Strength ignored (always full blur — snapshot-friendly).</summary>
        GlobalTexture,

        /// <summary>Do both: publish global texture AND composite into camera.</summary>
        Both,
    }
}