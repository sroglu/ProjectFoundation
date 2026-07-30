using UnityEngine;
using UnityEngine.UIElements;

namespace PFound.UISystem.Core
{
    /// <summary>
    /// Automatic static initializer for ThemeManager. Loads ThemeData and StyleSheet
    /// assets from Resources/UISystem/ at application startup (before any scene loads).
    ///
    /// Expected Resources layout:
    ///   Resources/UISystem/DefaultLight   (ThemeData)
    ///   Resources/UISystem/DefaultDark    (ThemeData)
    ///   Resources/UISystem/light          (StyleSheet)
    ///   Resources/UISystem/dark           (StyleSheet)
    ///   Resources/UISystem/DefaultTypography (TypographyConfig) [optional]
    /// </summary>
    public static class ThemeBootstrapper
    {
        private const string LightThemePath     = "UISystem/DefaultLight";
        private const string DarkThemePath      = "UISystem/DefaultDark";
        private const string LightSheetPath     = "UISystem/light";
        private const string DarkSheetPath      = "UISystem/dark";
        private const string TypographyPath     = "UISystem/DefaultTypography";
        private const string DefaultFontPath    = "UISystem/Roboto-Regular";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (ThemeManager.IsInitialized) return;

            var lightTheme  = Resources.Load<ThemeData>(LightThemePath);
            var darkTheme   = Resources.Load<ThemeData>(DarkThemePath);
            var lightSheet  = Resources.Load<StyleSheet>(LightSheetPath);
            var darkSheet   = Resources.Load<StyleSheet>(DarkSheetPath);
            var typography  = Resources.Load<TypographyConfig>(TypographyPath);
            var defaultFont = Resources.Load<Font>(DefaultFontPath);

            if (lightTheme == null && darkTheme == null)
            {
                // Fallback: try to find any ThemeData asset
                lightTheme = Resources.FindObjectsOfTypeAll<ThemeData>().Length > 0
                    ? Resources.FindObjectsOfTypeAll<ThemeData>()[0]
                    : null;
            }

            if (lightTheme == null && darkTheme == null)
            {
                Debug.LogWarning("[UISystem] ThemeBootstrapper: No ThemeData found in Resources/UISystem/. " +
                                 "Theme system will not work until ThemeManager.Initialize() is called manually.");
                return;
            }

            ThemeManager.Initialize(lightTheme, darkTheme, lightSheet, darkSheet, typography, defaultFont);
        }
    }
}
