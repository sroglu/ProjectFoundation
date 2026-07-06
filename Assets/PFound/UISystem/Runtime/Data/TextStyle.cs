using System;

namespace PFound.UISystem.Data
{
    /// <summary>
    /// Defines the visual style for one typography role mapped to a USS class name.
    /// The USS class (e.g. "m3-title") is applied to VisualElements by TypographyResolver.
    /// </summary>
    [Serializable]
    public struct TextStyle
    {
        /// <summary>USS class name to apply (e.g. "m3-title", "m3-body").</summary>
        public string UssClassName;

        /// <summary>Font size in reference-resolution pixels (1080x1920). Informational only — actual size set in USS.</summary>
        public float FontSize;

        /// <summary>TMP line spacing in TMP units. Informational only.</summary>
        public float LineSpacing;

        /// <summary>TMP character spacing in TMP units. Informational only.</summary>
        public float CharSpacing;

        /// <summary>M3 letter spacing in em units (e.g. 0.1). Applied via USS letter-spacing.</summary>
        public float LetterSpacing;
    }
}
