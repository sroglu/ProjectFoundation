using System;
using UnityEngine;

namespace PFound.Render.Effects.Overdraw
{
    /// <summary>
    /// A single (threshold, color) pair for the <see cref="OverdrawRenderFeature"/> ramp.
    /// Pixels with accumulated overdraw value &gt;= <see cref="threshold"/> render with
    /// <see cref="color"/> (alpha honored in the composite).
    /// </summary>
    [Serializable]
    public struct OverdrawThresholdEntry
    {
        /// <summary>Minimum accumulator value at which this tier's color applies.</summary>
        public float threshold;

        /// <summary>RGBA color rendered for pixels meeting / exceeding the threshold. Alpha honored.</summary>
        public Color color;

        /// <summary>Constructs a tier with the given threshold + color.</summary>
        public OverdrawThresholdEntry(float threshold, Color color)
        {
            this.threshold = threshold;
            this.color = color;
        }
    }
}
