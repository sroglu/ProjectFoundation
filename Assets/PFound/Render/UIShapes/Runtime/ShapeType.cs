namespace PFound.Render.UIShapes
{
    /// <summary>
    /// Shape selector for the <c>Render/UI/Shape</c> shader. Integer values match
    /// the shader's <c>_ShapeType</c> property + the corresponding <c>SHAPE_TYPE_*</c> keyword.
    /// </summary>
    public enum ShapeType
    {
        /// <summary>Axis-aligned rectangle filling the entire RectTransform.</summary>
        Rect = 0,

        /// <summary>Rectangle with independent per-corner radii. Most common UI panel shape.</summary>
        RoundedRect = 1,

        /// <summary>Pill / stadium shape (half-circle caps on the larger axis).</summary>
        Capsule = 2,

        /// <summary>Ellipse / oval inscribed in the RectTransform; circle when width == height.</summary>
        Ellipse = 3,
    }

    /// <summary>
    /// Gradient mode for the <c>_GradientMode</c> shader property. Mode <see cref="Radial"/>
    /// requires the <c>GRADIENT_MODE_RADIAL</c> keyword to be enabled.
    /// </summary>
    public enum GradientMode
    {
        /// <summary>Two-color gradient along an axis defined by <c>_GradientAngle</c>.</summary>
        Linear = 0,

        /// <summary>Two-color radial gradient from the shape center outward, shaped by <c>_GradientFalloff</c>.</summary>
        Radial = 1,
    }

    /// <summary>
    /// Noise mode for the <c>_NoiseMode</c> shader property. Mode <see cref="Worley"/>
    /// requires the <c>NOISE_MODE_WORLEY</c> keyword to be enabled.
    /// </summary>
    public enum NoiseMode
    {
        /// <summary>Smooth Perlin-style gradient noise.</summary>
        Perlin = 0,

        /// <summary>Cellular Worley noise (F1 distance).</summary>
        Worley = 1,
    }
}
