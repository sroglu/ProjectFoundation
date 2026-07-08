namespace PFound.Render.Effects.Blur
{
    /// <summary>
    /// Downsample factor for the blur pass. Locked to mobile-friendly powers of two via enum
    /// values that map directly to integer divisors.
    /// </summary>
    public enum BlurDownsample : int
    {
        /// <summary>No downsampling — full resolution. Sharpest blur, most expensive.</summary>
        x1 = 1,
        /// <summary>Half resolution per axis (~¼ pixel cost). Good desktop default.</summary>
        x2 = 2,
        /// <summary>Quarter resolution per axis (~1/16 pixel cost). Good mobile default.</summary>
        x4 = 4,
        /// <summary>Eighth resolution per axis (~1/64 pixel cost). Aggressive mobile budget.</summary>
        x8 = 8,
    }
}