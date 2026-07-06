using UnityEngine;

namespace PFound.TweenPresetLibrary
{
    /// <summary>
    /// The mutable surface a <see cref="TweenPresetPlayer"/> drives — one property per channel kind. Abstracted so the
    /// player is pure C# and unit-testable against a fake, with <see cref="ComponentTweenTarget"/> as the real
    /// Transform/CanvasGroup adapter.
    /// </summary>
    public interface ITweenTarget
    {
        /// <summary>Fade alpha (e.g. a CanvasGroup).</summary>
        float Alpha { get; set; }

        /// <summary>Local scale.</summary>
        Vector3 Scale { get; set; }

        /// <summary>2D position (anchored for a RectTransform, else local x/y).</summary>
        Vector2 Position { get; set; }

        /// <summary>Local euler angles (degrees).</summary>
        Vector3 EulerAngles { get; set; }
    }
}
