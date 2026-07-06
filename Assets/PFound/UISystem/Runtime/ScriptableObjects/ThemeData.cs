using PFound.UISystem.Data;
using PFound.UISystem.Enums;
using Sirenix.OdinInspector;
using UnityEngine;

namespace PFound.UISystem
{
    /// <summary>
    /// Single ScriptableObject holding the full M3 token set for one theme variant
    /// (light or dark). All UISystem components read from the active ThemeData via
    /// ThemeManager.ActiveTheme.
    /// </summary>
    [CreateAssetMenu(menuName = "UISystem/Theme Data", fileName = "ThemeData")]
    public class ThemeData : ScriptableObject
    {
        // ------------------------------------------------------------------ //
        //  Color Palette                                                       //
        // ------------------------------------------------------------------ //
        [BoxGroup("Colors")]
        [Tooltip("Full M3 color palette for this theme variant.")]
        [SerializeField] private ColorPalette _colors;

        // ------------------------------------------------------------------ //
        //  Elevation Presets (0–5)                                             //
        // ------------------------------------------------------------------ //
        [BoxGroup("Elevation")]
        [Tooltip("Six elevation levels (index 0–5). Index matches ElevationLevel property on SDFRectGraphic.")]
        [SerializeField] private ElevationPreset[] _elevationPresets = new ElevationPreset[6];

        // ------------------------------------------------------------------ //
        //  Shape Presets                                                       //
        // ------------------------------------------------------------------ //
        [BoxGroup("Shape")]
        [SerializeField] private ShapePresets _shapes = ShapePresets.Default;

        // ------------------------------------------------------------------ //
        //  Dynamic Color Seed (informational — records generation source)      //
        // ------------------------------------------------------------------ //
        [BoxGroup("Dynamic Color")]
        [Tooltip("Seed color used to generate this theme via DynamicColorGenerator. Informational only — does not affect runtime.")]
        [ShowIf("@_seedColor != default(UnityEngine.Color)")]
        [SerializeField] private Color _seedColor;

        // ------------------------------------------------------------------ //
        //  Motion Presets (Emphasized, Standard, EmphasizedDecelerate,        //
        //  StandardDecelerate)                                                 //
        // ------------------------------------------------------------------ //
        [BoxGroup("Motion")]
        [Tooltip("Four motion styles indexed by MotionPresetType enum.")]
        [SerializeField] private MotionPreset[] _motionPresets = new MotionPreset[4];

        // ------------------------------------------------------------------ //
        //  Public Accessors                                                    //
        // ------------------------------------------------------------------ //
        public ColorPalette  Colors          => _colors;
        public ShapePresets  Shapes          => _shapes;

        /// <summary>Returns the fill color for the given semantic color role.</summary>
        public Color GetColor(ColorRole role)
        {
            switch (role)
            {
                case ColorRole.Primary:               return _colors.Primary;
                case ColorRole.OnPrimary:             return _colors.OnPrimary;
                case ColorRole.PrimaryContainer:      return _colors.PrimaryContainer;
                case ColorRole.OnPrimaryContainer:    return _colors.OnPrimaryContainer;
                case ColorRole.Secondary:             return _colors.Secondary;
                case ColorRole.OnSecondary:           return _colors.OnSecondary;
                case ColorRole.SecondaryContainer:    return _colors.SecondaryContainer;
                case ColorRole.OnSecondaryContainer:  return _colors.OnSecondaryContainer;
                case ColorRole.Surface:               return _colors.Surface;
                case ColorRole.OnSurface:             return _colors.OnSurface;
                case ColorRole.SurfaceVariant:        return _colors.SurfaceVariant;
                case ColorRole.OnSurfaceVariant:      return _colors.OnSurfaceVariant;
                case ColorRole.Error:                 return _colors.Error;
                case ColorRole.OnError:               return _colors.OnError;
                case ColorRole.Outline:               return _colors.Outline;
                case ColorRole.OutlineVariant:        return _colors.OutlineVariant;
                case ColorRole.Background:            return _colors.Background;
                case ColorRole.InverseSurface:         return _colors.InverseSurface;
                case ColorRole.InverseOnSurface:       return _colors.InverseOnSurface;
                case ColorRole.InversePrimary:         return _colors.InversePrimary;
                case ColorRole.Tertiary:               return _colors.Tertiary;
                case ColorRole.OnTertiary:             return _colors.OnTertiary;
                case ColorRole.TertiaryContainer:      return _colors.TertiaryContainer;
                case ColorRole.OnTertiaryContainer:    return _colors.OnTertiaryContainer;
                case ColorRole.SurfaceContainerLowest:  return _colors.SurfaceContainerLowest;
                case ColorRole.SurfaceContainer:        return _colors.SurfaceContainer;
                case ColorRole.SurfaceContainerLow:     return _colors.SurfaceContainerLow;
                case ColorRole.SurfaceContainerHigh:    return _colors.SurfaceContainerHigh;
                case ColorRole.SurfaceContainerHighest: return _colors.SurfaceContainerHighest;
                case ColorRole.Scrim:                   return _colors.Scrim;
                case ColorRole.SurfaceTint:             return _colors.SurfaceTint;
                case ColorRole.PrimaryFixed:            return _colors.PrimaryFixed;
                case ColorRole.PrimaryFixedDim:         return _colors.PrimaryFixedDim;
                case ColorRole.OnPrimaryFixed:          return _colors.OnPrimaryFixed;
                case ColorRole.OnPrimaryFixedVariant:   return _colors.OnPrimaryFixedVariant;
                case ColorRole.SecondaryFixed:          return _colors.SecondaryFixed;
                case ColorRole.SecondaryFixedDim:       return _colors.SecondaryFixedDim;
                case ColorRole.OnSecondaryFixed:        return _colors.OnSecondaryFixed;
                case ColorRole.OnSecondaryFixedVariant: return _colors.OnSecondaryFixedVariant;
                case ColorRole.TertiaryFixed:           return _colors.TertiaryFixed;
                case ColorRole.TertiaryFixedDim:        return _colors.TertiaryFixedDim;
                case ColorRole.OnTertiaryFixed:         return _colors.OnTertiaryFixed;
                case ColorRole.OnTertiaryFixedVariant:  return _colors.OnTertiaryFixedVariant;
                case ColorRole.ErrorContainer:          return _colors.ErrorContainer;
                case ColorRole.OnErrorContainer:        return _colors.OnErrorContainer;
                case ColorRole.SurfaceDim:              return _colors.SurfaceDim;
                case ColorRole.SurfaceBright:           return _colors.SurfaceBright;
                case ColorRole.Shadow:                  return _colors.Shadow;
                default:                               return Color.magenta; // indicates missing mapping
            }
        }

        /// <summary>Returns the elevation preset for levels 0–5 (clamped).</summary>
        public ElevationPreset GetElevation(int level)
        {
            if (_elevationPresets == null || _elevationPresets.Length == 0)
                return default;
            return _elevationPresets[Mathf.Clamp(level, 0, _elevationPresets.Length - 1)];
        }

        /// <summary>Returns the motion preset for the given type.</summary>
        public MotionPreset GetMotion(MotionPresetType type)
        {
            int index = (int)type;
            if (_motionPresets == null || index < 0 || index >= _motionPresets.Length)
                return default;
            return _motionPresets[index];
        }

        // ------------------------------------------------------------------ //
        //  Editor Validation                                                   //
        // ------------------------------------------------------------------ //
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_elevationPresets == null || _elevationPresets.Length != 6)
            {
                var resized = new ElevationPreset[6];
                if (_elevationPresets != null)
                    for (int i = 0; i < Mathf.Min(_elevationPresets.Length, 6); i++)
                        resized[i] = _elevationPresets[i];
                _elevationPresets = resized;
            }

            if (_motionPresets == null || _motionPresets.Length != 4)
            {
                var resized = new MotionPreset[4];
                if (_motionPresets != null)
                    for (int i = 0; i < Mathf.Min(_motionPresets.Length, 4); i++)
                        resized[i] = _motionPresets[i];
                _motionPresets = resized;
            }
        }
#endif
    }
}
