using System;
using UnityEngine;

namespace PFound.UISystem.Shapes
{
    /// <summary>
    /// Runtime-callable helpers for keeping <c>UISystem/Shape</c> material state consistent
    /// between the <c>_&lt;Effect&gt;Enable</c> float properties and the matching shader keywords.
    /// The Inspector calls these too — single source of truth for keyword/property sync.
    /// </summary>
    /// <remarks>
    /// The keyword/property duality exists because:
    /// <list type="bullet">
    ///   <item>The shader's branching uses keywords (preprocessor #ifdef), so flipping the
    ///         property alone doesn't change which code path the shader takes.</item>
    ///   <item>The Material Inspector's [Toggle()] PropertyDrawer hides the keyword from
    ///         users — but programmatic consumers can desync them by accident.</item>
    /// </list>
    /// Always go through this helper to avoid the "property says ON but keyword says OFF" bug.
    /// </remarks>
    public static class UIShapeMaterialHelpers
    {
        /// <summary>
        /// Sets both the effect enable property AND the matching shader keyword in lockstep.
        /// </summary>
        public static void SetEffectEnabled(Material material, string enableProp, string keyword, bool enabled)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            if (string.IsNullOrEmpty(enableProp)) throw new ArgumentException("Property name required", nameof(enableProp));
            if (string.IsNullOrEmpty(keyword)) throw new ArgumentException("Keyword required", nameof(keyword));

            material.SetFloat(enableProp, enabled ? 1f : 0f);
            if (enabled) material.EnableKeyword(keyword);
            else material.DisableKeyword(keyword);
        }

        /// <summary>
        /// Toggles the matching <c>SHAPE_TYPE_*</c> shader keyword for <paramref name="type"/>.
        /// All four exclusive keywords are disabled, then the requested one is enabled.
        /// RoundedRect is the shader's default <c>else</c> branch — no keyword required there.
        /// </summary>
        public static void SyncShapeTypeKeyword(Material material, ShapeType type)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));

            material.DisableKeyword(UIShapeShaderKeywords.ShapeTypeRect);
            material.DisableKeyword(UIShapeShaderKeywords.ShapeTypeRoundedRect);
            material.DisableKeyword(UIShapeShaderKeywords.ShapeTypeCapsule);
            material.DisableKeyword(UIShapeShaderKeywords.ShapeTypeEllipse);

            switch (type)
            {
                case ShapeType.Rect:
                    material.EnableKeyword(UIShapeShaderKeywords.ShapeTypeRect);
                    break;
                case ShapeType.Capsule:
                    material.EnableKeyword(UIShapeShaderKeywords.ShapeTypeCapsule);
                    break;
                case ShapeType.Ellipse:
                    material.EnableKeyword(UIShapeShaderKeywords.ShapeTypeEllipse);
                    break;
            }
        }

        /// <summary>
        /// Clamps each component of <paramref name="raw"/> to <c>[0, halfMinDimension]</c>.
        /// Negative inputs become 0; over-range inputs become <paramref name="halfMinDimension"/>.
        /// </summary>
        public static Vector4 ClampRadii(in Vector4 raw, float halfMinDimension)
        {
            float maxR = Mathf.Max(halfMinDimension, 0f);
            return new Vector4(
                Mathf.Clamp(raw.x, 0f, maxR),
                Mathf.Clamp(raw.y, 0f, maxR),
                Mathf.Clamp(raw.z, 0f, maxR),
                Mathf.Clamp(raw.w, 0f, maxR));
        }
    }
}
