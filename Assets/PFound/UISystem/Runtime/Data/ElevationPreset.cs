using System;
using UnityEngine;

namespace PFound.UISystem.Data
{
    /// <summary>
    /// Visual parameters for one of M3's 6 elevation levels (0–5).
    /// </summary>
    [Serializable]
    public struct ElevationPreset
    {
        /// <summary>Shadow offset in reference-resolution pixels (1080x1920).</summary>
        public Vector2 ShadowOffset;

        /// <summary>Shadow blur/spread radius in reference-resolution pixels.</summary>
        public float ShadowBlur;

        /// <summary>Shadow color (typically black with low alpha).</summary>
        public Color ShadowColor;

        /// <summary>
        /// Tonal overlay alpha that tints the surface with the Primary color
        /// to convey elevation (0 = none, ~0.14 = level 5).
        /// </summary>
        [Range(0f, 1f)]
        public float TonalOverlayAlpha;
    }
}
