using PFound.UISystem;
using PFound.UISystem.Data;
using PFound.UISystem.Enums;
using UnityEditor;
using UnityEngine;

namespace PFound.UISystem.Editor
{
    /// <summary>
    /// Factory methods for DefaultLight, DefaultDark, and DefaultTypography assets.
    /// Auto-creation is driven by <see cref="UISystemAssetProvider"/> via GameSpecificAssetGuard
    /// (Utilities.EditorHelpers). This class provides the initialization callbacks and a manual
    /// "Assets/UISystem/Create Default Assets" MenuItem.
    /// </summary>
    internal static class DefaultAssetsSetup
    {
        private const string GameSpecificRoot  = "Assets/GameSpecific";
        private const string ResourcesRoot     = GameSpecificRoot + "/UISystem/Resources/UISystem";
        private const string LightPath         = ResourcesRoot + "/DefaultLight.asset";
        private const string DarkPath          = ResourcesRoot + "/DefaultDark.asset";
        private const string TypoPath          = ResourcesRoot + "/DefaultTypography.asset";

        [MenuItem("Assets/UISystem/Create Default Assets")]
        public static void CreateMissingAssets()
        {
            bool changed = false;
            changed |= EnsureThemeAssets();
            changed |= EnsureTypographyAsset();

            if (changed)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[UISystem] Default assets created.");
            }
        }

        // ------------------------------------------------------------------ //
        //  Factory methods (called by UISystemAssetProvider via Guard)        //
        // ------------------------------------------------------------------ //

        public static void ConfigureTheme(ScriptableObject so, bool isLight)
        {
            SetField(so, "_colors",          isLight ? LightPalette() : DarkPalette());
            SetField(so, "_elevationPresets", BuildElevationPresets());
            SetField(so, "_shapes",           ShapePresets.Default);
            SetField(so, "_motionPresets",    BuildMotionPresets());
        }

        public static void ConfigureTypography(ScriptableObject so)
        {
            SetField(so, "_displayLarge",   BuildTextStyle("m3-display-large",   57f, -0.25f));
            SetField(so, "_displayMedium",  BuildTextStyle("m3-display-medium",  45f,  0f));
            SetField(so, "_displaySmall",   BuildTextStyle("m3-display-small",   36f,  0f));
            SetField(so, "_headlineLarge",  BuildTextStyle("m3-headline-large",  32f,  0f));
            SetField(so, "_headlineMedium", BuildTextStyle("m3-headline-medium", 28f,  0f));
            SetField(so, "_headlineSmall",  BuildTextStyle("m3-headline-small",  24f,  0f));
            SetField(so, "_titleLarge",     BuildTextStyle("m3-title-large",     22f,  0f));
            SetField(so, "_titleMedium",    BuildTextStyle("m3-title-medium",    16f,  0.15f));
            SetField(so, "_titleSmall",     BuildTextStyle("m3-title-small",     14f,  0.1f));
            SetField(so, "_bodyLarge",      BuildTextStyle("m3-body-large",      16f,  0.5f));
            SetField(so, "_bodyMedium",     BuildTextStyle("m3-body-medium",     14f,  0.25f));
            SetField(so, "_bodySmall",      BuildTextStyle("m3-body-small",      12f,  0.4f));
            SetField(so, "_labelLarge",     BuildTextStyle("m3-label-large",     14f,  0.1f));
            SetField(so, "_labelMedium",    BuildTextStyle("m3-label-medium",    12f,  0.5f));
            SetField(so, "_labelSmall",     BuildTextStyle("m3-label-small",     11f,  0.5f));
        }

        // ------------------------------------------------------------------ //
        //  MenuItem helpers                                                   //
        // ------------------------------------------------------------------ //

        private static bool EnsureThemeAssets()
        {
            bool changed = false;
            EnsureFolder(ResourcesRoot);

            if (AssetDatabase.LoadAssetAtPath<ThemeData>(LightPath) == null)
            {
                var asset = ScriptableObject.CreateInstance<ThemeData>();
                ConfigureTheme(asset, isLight: true);
                AssetDatabase.CreateAsset(asset, LightPath);
                changed = true;
            }

            if (AssetDatabase.LoadAssetAtPath<ThemeData>(DarkPath) == null)
            {
                var asset = ScriptableObject.CreateInstance<ThemeData>();
                ConfigureTheme(asset, isLight: false);
                AssetDatabase.CreateAsset(asset, DarkPath);
                changed = true;
            }

            return changed;
        }

        private static bool EnsureTypographyAsset()
        {
            EnsureFolder(ResourcesRoot);
            if (AssetDatabase.LoadAssetAtPath<TypographyConfig>(TypoPath) != null) return false;

            var asset = ScriptableObject.CreateInstance<TypographyConfig>();
            ConfigureTypography(asset);
            AssetDatabase.CreateAsset(asset, TypoPath);
            return true;
        }

        // ------------------------------------------------------------------ //
        //  Data builders                                                      //
        // ------------------------------------------------------------------ //
        private static ColorPalette LightPalette() => new ColorPalette
        {
            Primary = Hex("#6750A4"), OnPrimary = Hex("#FFFFFF"),
            PrimaryContainer = Hex("#EADDFF"), OnPrimaryContainer = Hex("#21005D"),
            Secondary = Hex("#625B71"), OnSecondary = Hex("#FFFFFF"),
            SecondaryContainer = Hex("#E8DEF8"), OnSecondaryContainer = Hex("#1D192B"),
            Surface = Hex("#FFFBFE"), OnSurface = Hex("#1C1B1F"),
            SurfaceVariant = Hex("#E7E0EC"), OnSurfaceVariant = Hex("#49454F"),
            Error = Hex("#B3261E"), OnError = Hex("#FFFFFF"),
            Outline = Hex("#79747E"), OutlineVariant = Hex("#CAC4D0"),
            Background = Hex("#FFFBFE"),
            InverseSurface = Hex("#313033"), InverseOnSurface = Hex("#F4EFF4"), InversePrimary = Hex("#D0BCFF"),
            Tertiary = Hex("#7D5260"), OnTertiary = Hex("#FFFFFF"),
            TertiaryContainer = Hex("#FFD8E4"), OnTertiaryContainer = Hex("#31111D"),
            SurfaceContainerLowest = Hex("#FFFFFF"), SurfaceContainerLow = Hex("#F7F2FA"),
            SurfaceContainer = Hex("#F3EDF7"), SurfaceContainerHigh = Hex("#ECE6F0"),
            SurfaceContainerHighest = Hex("#E6E0E9"),
            Scrim = new Color(0f, 0f, 0f, 0.32f), SurfaceTint = Hex("#6750A4"),
            PrimaryFixed = Hex("#F2DAFF"), PrimaryFixedDim = Hex("#D9B9FF"),
            OnPrimaryFixed = Hex("#21005D"), OnPrimaryFixedVariant = Hex("#4F378B"),
            SecondaryFixed = Hex("#E8DEF8"), SecondaryFixedDim = Hex("#D0C4E8"),
            OnSecondaryFixed = Hex("#1D192B"), OnSecondaryFixedVariant = Hex("#4A4458"),
            TertiaryFixed = Hex("#FFD8E4"), TertiaryFixedDim = Hex("#EFB8C8"),
            OnTertiaryFixed = Hex("#31111D"), OnTertiaryFixedVariant = Hex("#633B48"),
            ErrorContainer = Hex("#F9DEDC"), OnErrorContainer = Hex("#410E0B"),
            SurfaceDim = Hex("#DED8E1"), SurfaceBright = Hex("#FFF7FF"),
            Shadow = new Color(0f, 0f, 0f, 1f),
        };

        private static ColorPalette DarkPalette() => new ColorPalette
        {
            Primary = Hex("#D0BCFF"), OnPrimary = Hex("#381E72"),
            PrimaryContainer = Hex("#4F378B"), OnPrimaryContainer = Hex("#EADDFF"),
            Secondary = Hex("#CCC2DC"), OnSecondary = Hex("#332D41"),
            SecondaryContainer = Hex("#4A4458"), OnSecondaryContainer = Hex("#E8DEF8"),
            Surface = Hex("#1C1B1F"), OnSurface = Hex("#E6E1E5"),
            SurfaceVariant = Hex("#49454F"), OnSurfaceVariant = Hex("#CAC4D0"),
            Error = Hex("#F2B8B5"), OnError = Hex("#601410"),
            Outline = Hex("#938F99"), OutlineVariant = Hex("#49454F"),
            Background = Hex("#1C1B1F"),
            InverseSurface = Hex("#E6E1E5"), InverseOnSurface = Hex("#313033"), InversePrimary = Hex("#6750A4"),
            Tertiary = Hex("#EFB8C8"), OnTertiary = Hex("#492532"),
            TertiaryContainer = Hex("#633B48"), OnTertiaryContainer = Hex("#FFD8E4"),
            SurfaceContainerLowest = Hex("#0F0E13"), SurfaceContainerLow = Hex("#1E1D22"),
            SurfaceContainer = Hex("#221F26"), SurfaceContainerHigh = Hex("#27262B"),
            SurfaceContainerHighest = Hex("#2C2B31"),
            Scrim = new Color(0f, 0f, 0f, 0.32f), SurfaceTint = Hex("#D0BCFF"),
            PrimaryFixed = Hex("#F2DAFF"), PrimaryFixedDim = Hex("#D9B9FF"),
            OnPrimaryFixed = Hex("#21005D"), OnPrimaryFixedVariant = Hex("#4F378B"),
            SecondaryFixed = Hex("#E8DEF8"), SecondaryFixedDim = Hex("#D0C4E8"),
            OnSecondaryFixed = Hex("#1D192B"), OnSecondaryFixedVariant = Hex("#4A4458"),
            TertiaryFixed = Hex("#FFD8E4"), TertiaryFixedDim = Hex("#EFB8C8"),
            OnTertiaryFixed = Hex("#31111D"), OnTertiaryFixedVariant = Hex("#633B48"),
            ErrorContainer = Hex("#8C1D18"), OnErrorContainer = Hex("#F2B8B5"),
            SurfaceDim = Hex("#141218"), SurfaceBright = Hex("#3B383E"),
            Shadow = new Color(0f, 0f, 0f, 1f),
        };

        private static ElevationPreset[] BuildElevationPresets() => new[]
        {
            new ElevationPreset { ShadowOffset = Vector2.zero,        ShadowBlur = 0f,  ShadowColor = new Color(0,0,0,0f),    TonalOverlayAlpha = 0f },
            new ElevationPreset { ShadowOffset = new Vector2(0,-2f),  ShadowBlur = 4f,  ShadowColor = new Color(0,0,0,0.12f), TonalOverlayAlpha = 0.05f },
            new ElevationPreset { ShadowOffset = new Vector2(0,-4f),  ShadowBlur = 8f,  ShadowColor = new Color(0,0,0,0.16f), TonalOverlayAlpha = 0.08f },
            new ElevationPreset { ShadowOffset = new Vector2(0,-6f),  ShadowBlur = 12f, ShadowColor = new Color(0,0,0,0.20f), TonalOverlayAlpha = 0.11f },
            new ElevationPreset { ShadowOffset = new Vector2(0,-8f),  ShadowBlur = 16f, ShadowColor = new Color(0,0,0,0.24f), TonalOverlayAlpha = 0.12f },
            new ElevationPreset { ShadowOffset = new Vector2(0,-12f), ShadowBlur = 24f, ShadowColor = new Color(0,0,0,0.30f), TonalOverlayAlpha = 0.14f },
        };

        private static MotionPreset[] BuildMotionPresets()
        {
            var linear  = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            var easeOut = new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 2f), new Keyframe(1f, 1f, 0f, 0f));
            return new[]
            {
                new MotionPreset { Curve = easeOut, DurationMs = 500f },
                new MotionPreset { Curve = linear,  DurationMs = 300f },
                new MotionPreset { Curve = easeOut, DurationMs = 400f },
                new MotionPreset { Curve = linear,  DurationMs = 200f },
            };
        }

        private static TextStyle BuildTextStyle(string ussClass, float size, float letterSpacing = 0f) => new TextStyle
        {
            UssClassName = ussClass, FontSize = size,
            LineSpacing = 0f, CharSpacing = 0f, LetterSpacing = letterSpacing,
        };

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
                string child  = System.IO.Path.GetFileName(path);
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static void SetField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(obj, value);
        }
    }
}
