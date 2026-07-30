using System;
using UnityEngine;

namespace PFound.UISystem.Data
{
    /// <summary>
    /// An animation curve + duration defining one of M3's motion styles.
    /// </summary>
    [Serializable]
    public struct MotionPreset
    {
        /// <summary>Easing curve for the animation.</summary>
        public AnimationCurve Curve;

        /// <summary>Total duration in milliseconds.</summary>
        public float DurationMs;

        public float DurationSeconds => DurationMs / 1000f;

        public MotionPreset(float durationMs, AnimationCurve curve = null)
        {
            DurationMs = durationMs;
            Curve      = curve ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
        }
    }
}
