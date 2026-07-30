using PFound.UISystem.Data;
using UnityEditor;
using UnityEngine;

namespace PFound.UISystem.Editor.ColorGeneration
{
    /// <summary>
    /// Generates M3 Dynamic Color theme assets from a single seed color.
    ///
    /// Produces a matching light + dark ThemeData asset pair where:
    ///   - Primary palette: derived directly from the seed
    ///   - Secondary palette: same hue, reduced chroma (~seed/3)
    ///   - Tertiary palette: hue rotated +60°, reduced chroma (~seed/2)
    ///   - Neutral palette: same hue, very low chroma (~4)
    ///   - NeutralVariant palette: same hue, low chroma (~8)
    ///   - Error palette: fixed hue 25 (M3 red), chroma 84
    ///
    /// Usage:
    ///   DynamicColorGenerator.GenerateFromSeed(
    ///       seed: new Color(0.404f, 0.314f, 0.643f),  // #6750A4
    ///       outputFolder: "Assets/UISystem/Assets/Themes/",
    ///       baseName: "Generated"
    ///   );
    /// </summary>
    public static class DynamicColorGenerator
    {
        /// <summary>
        /// Generates DefaultLight.asset and DefaultDark.asset from a seed color.
        /// </summary>
        /// <param name="seed">The seed color (sRGB).</param>
        /// <param name="outputFolder">Asset folder path (must end with /).</param>
        /// <param name="baseName">Asset name prefix — produces {baseName}Light.asset + {baseName}Dark.asset.</param>
        public static void GenerateFromSeed(Color seed, string outputFolder, string baseName = "Generated")
        {
            var primary       = TonalPalette.FromColor(seed);
            var secondary     = TonalPalette.SecondaryFromColor(seed);
            var tertiary      = TonalPalette.TertiaryFromColor(seed);
            var neutral       = TonalPalette.NeutralFromColor(seed);
            var neutralVar    = TonalPalette.NeutralVariantFromColor(seed);
            var error         = TonalPalette.Error();

            var light = BuildTheme(primary, secondary, tertiary, neutral, neutralVar, error, isLight: true,  seedColor: seed);
            var dark  = BuildTheme(primary, secondary, tertiary, neutral, neutralVar, error, isLight: false, seedColor: seed);

            EnsureFolder(outputFolder);

            string lightPath = outputFolder + baseName + "Light.asset";
            string darkPath  = outputFolder + baseName + "Dark.asset";

            SaveTheme(light, lightPath);
            SaveTheme(dark,  darkPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[UISystem] Generated themes from seed {ColorUtility.ToHtmlStringRGB(seed)}:\n  {lightPath}\n  {darkPath}");
        }

        // ------------------------------------------------------------------ //
        //  Theme construction                                                  //
        // ------------------------------------------------------------------ //

        private static PFound.UISystem.ThemeData BuildTheme(
            TonalPalette primary, TonalPalette secondary,
            TonalPalette tertiary, TonalPalette neutral, TonalPalette neutralVar,
            TonalPalette error, bool isLight, Color seedColor)
        {
            var asset = ScriptableObject.CreateInstance<PFound.UISystem.ThemeData>();

            var palette = isLight ? BuildLightPalette(primary, secondary, neutral, neutralVar, error)
                                  : BuildDarkPalette(primary, secondary, neutral, neutralVar, error);

            SetField(asset, "_colors",          palette);
            SetField(asset, "_elevationPresets", BuildElevationPresets());
            SetField(asset, "_shapes",           PFound.UISystem.Data.ShapePresets.Default);
            SetField(asset, "_motionPresets",    BuildMotionPresets());
            SetField(asset, "_seedColor",        seedColor);

            return asset;
        }

        private static ColorPalette BuildLightPalette(
            TonalPalette p, TonalPalette s, TonalPalette n, TonalPalette nv, TonalPalette e)
            => new ColorPalette
            {
                Primary              = p.Tone(40f),
                OnPrimary            = p.Tone(100f),
                PrimaryContainer     = p.Tone(90f),
                OnPrimaryContainer   = p.Tone(10f),
                Secondary            = s.Tone(40f),
                OnSecondary          = s.Tone(100f),
                SecondaryContainer   = s.Tone(90f),
                OnSecondaryContainer = s.Tone(10f),
                Surface              = n.Tone(99f),
                OnSurface            = n.Tone(10f),
                SurfaceVariant       = nv.Tone(90f),
                OnSurfaceVariant     = nv.Tone(30f),
                SurfaceContainerLowest  = n.Tone(100f),
                SurfaceContainerLow     = n.Tone(96f),
                SurfaceContainer        = n.Tone(94f),
                SurfaceContainerHigh    = n.Tone(92f),
                SurfaceContainerHighest = n.Tone(90f),
                Error                = e.Tone(40f),
                OnError              = e.Tone(100f),
                Outline              = nv.Tone(50f),
                OutlineVariant       = nv.Tone(80f),
                Background           = n.Tone(99f),
                InverseSurface       = n.Tone(20f),
                InverseOnSurface     = n.Tone(95f),
                InversePrimary       = p.Tone(80f),
            };

        private static ColorPalette BuildDarkPalette(
            TonalPalette p, TonalPalette s, TonalPalette n, TonalPalette nv, TonalPalette e)
            => new ColorPalette
            {
                Primary              = p.Tone(80f),
                OnPrimary            = p.Tone(20f),
                PrimaryContainer     = p.Tone(30f),
                OnPrimaryContainer   = p.Tone(90f),
                Secondary            = s.Tone(80f),
                OnSecondary          = s.Tone(20f),
                SecondaryContainer   = s.Tone(30f),
                OnSecondaryContainer = s.Tone(90f),
                Surface              = n.Tone(10f),
                OnSurface            = n.Tone(90f),
                SurfaceVariant       = nv.Tone(30f),
                OnSurfaceVariant     = nv.Tone(80f),
                SurfaceContainerLowest  = n.Tone(4f),
                SurfaceContainerLow     = n.Tone(10f),
                SurfaceContainer        = n.Tone(12f),
                SurfaceContainerHigh    = n.Tone(17f),
                SurfaceContainerHighest = n.Tone(22f),
                Error                = e.Tone(80f),
                OnError              = e.Tone(20f),
                Outline              = nv.Tone(60f),
                OutlineVariant       = nv.Tone(30f),
                Background           = n.Tone(10f),
                InverseSurface       = n.Tone(90f),
                InverseOnSurface     = n.Tone(20f),
                InversePrimary       = p.Tone(40f),
            };

        // ------------------------------------------------------------------ //
        //  Shared presets (same as DefaultAssetsSetup)                        //
        // ------------------------------------------------------------------ //

        private static PFound.UISystem.Data.ElevationPreset[] BuildElevationPresets() => new[]
        {
            new PFound.UISystem.Data.ElevationPreset { ShadowOffset = Vector2.zero,        ShadowBlur = 0f,  ShadowColor = new Color(0,0,0,0f),    TonalOverlayAlpha = 0f },
            new PFound.UISystem.Data.ElevationPreset { ShadowOffset = new Vector2(0,-2f),  ShadowBlur = 4f,  ShadowColor = new Color(0,0,0,0.12f), TonalOverlayAlpha = 0.05f },
            new PFound.UISystem.Data.ElevationPreset { ShadowOffset = new Vector2(0,-4f),  ShadowBlur = 8f,  ShadowColor = new Color(0,0,0,0.16f), TonalOverlayAlpha = 0.08f },
            new PFound.UISystem.Data.ElevationPreset { ShadowOffset = new Vector2(0,-6f),  ShadowBlur = 12f, ShadowColor = new Color(0,0,0,0.20f), TonalOverlayAlpha = 0.11f },
            new PFound.UISystem.Data.ElevationPreset { ShadowOffset = new Vector2(0,-8f),  ShadowBlur = 16f, ShadowColor = new Color(0,0,0,0.24f), TonalOverlayAlpha = 0.12f },
            new PFound.UISystem.Data.ElevationPreset { ShadowOffset = new Vector2(0,-12f), ShadowBlur = 24f, ShadowColor = new Color(0,0,0,0.30f), TonalOverlayAlpha = 0.14f },
        };

        private static PFound.UISystem.Data.MotionPreset[] BuildMotionPresets()
        {
            var linear  = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            var easeOut = new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 2f), new Keyframe(1f, 1f, 0f, 0f));
            return new[]
            {
                new PFound.UISystem.Data.MotionPreset { Curve = easeOut, DurationMs = 500f },
                new PFound.UISystem.Data.MotionPreset { Curve = linear,  DurationMs = 300f },
                new PFound.UISystem.Data.MotionPreset { Curve = easeOut, DurationMs = 400f },
                new PFound.UISystem.Data.MotionPreset { Curve = linear,  DurationMs = 200f },
            };
        }

        // ------------------------------------------------------------------ //
        //  Helpers                                                             //
        // ------------------------------------------------------------------ //

        private static void SaveTheme(PFound.UISystem.ThemeData asset, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<PFound.UISystem.ThemeData>(path);
            if (existing != null)
            {
                // Update existing — copy fields via reflection
                CopyFields(asset, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(asset);
            }
            else
            {
                AssetDatabase.CreateAsset(asset, path);
            }
        }

        private static void CopyFields(PFound.UISystem.ThemeData src, PFound.UISystem.ThemeData dst)
        {
            foreach (var field in typeof(PFound.UISystem.ThemeData).GetFields(
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
            {
                field.SetValue(dst, field.GetValue(src));
            }
        }

        private static void SetField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(obj, value);
        }

        private static void EnsureFolder(string path)
        {
            path = path.TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
                string child  = System.IO.Path.GetFileName(path);
                if (parent != null) AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
