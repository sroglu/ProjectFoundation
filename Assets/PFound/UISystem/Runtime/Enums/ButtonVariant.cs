namespace PFound.UISystem.Enums
{
    /// <summary>
    /// M3-style button visual variants. Explicit integer values prevent
    /// serialization breakage if the enum is reordered.
    /// </summary>
    public enum ButtonVariant
    {
        /// <summary>High-emphasis. Primary color background, pill shape, elevation shadow.</summary>
        Filled   = 0,

        /// <summary>Medium-emphasis. Transparent background with 1dp outline.</summary>
        Outlined = 1,

        /// <summary>Low-emphasis. Transparent background and border, primary color label.</summary>
        Text     = 2,

        /// <summary>Medium-emphasis. Secondary-container background, no shadow.</summary>
        Tonal    = 3
    }
}
