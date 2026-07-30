namespace PFound.UISystem.Enums
{
    /// <summary>
    /// M3 button corner shape.
    /// Round = full pill (border-radius 9999).
    /// Square = small rounded corners (border-radius 8dp, M3 spec).
    /// </summary>
    public enum ButtonShape
    {
        Round,   // Full pill — corner-radius = 9999 (clamped to height/2)
        Square,  // Rounded square — corner-radius = 8dp
    }
}
