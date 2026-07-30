using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PFound.UISystem.Core
{
    /// <summary>
    /// Static theme manager that holds the active ThemeData and broadcasts
    /// OnThemeChanged when the active theme is swapped.
    ///
    /// USS sync: On SetTheme(), ThemeManager swaps the light/dark USS StyleSheet on
    /// each managed panel's rootVisualElement. The stylesheets define all --m3-*
    /// custom properties in a :root { } block, which cascade to all descendants.
    /// Elements using var(--m3-*) update automatically when the stylesheet is swapped.
    ///
    /// Initialization: Call Initialize() from ThemeBootstrapper or manually before
    /// any UI is created. See ThemeBootstrapper for automatic setup from Resources.
    /// </summary>
    public static class ThemeManager
    {
        // ------------------------------------------------------------------ //
        //  State                                                              //
        // ------------------------------------------------------------------ //
        private static ThemeData       _lightTheme;
        private static ThemeData       _darkTheme;
        private static ThemeData       _activeTheme;
        private static TypographyConfig _typographyConfig;
        private static StyleSheet      _lightSheet;
        private static StyleSheet      _darkSheet;
        private static Font            _defaultFont;
        private static readonly List<WeakReference<UIDocument>> _managedPanels = new();
        private static bool            _initialized;

        // ------------------------------------------------------------------ //
        //  Public API                                                         //
        // ------------------------------------------------------------------ //

        /// <summary>Whether Initialize() has been called with valid data.</summary>
        public static bool IsInitialized => _initialized;

        /// <summary>Currently active theme. Null before Initialize.</summary>
        public static ThemeData ActiveTheme => _activeTheme;

        /// <summary>Typography configuration. Null if not set.</summary>
        public static TypographyConfig TypographyConfig => _typographyConfig;

        /// <summary>Fired on the same frame as SetTheme(). Arg is the new active ThemeData.</summary>
        public static event Action<ThemeData> OnThemeChanged;

        /// <summary>
        /// One-time setup. Call before any UI is created. Safe to call multiple times —
        /// subsequent calls update the references and re-sync all panels.
        /// </summary>
        public static void Initialize(
            ThemeData lightTheme,
            ThemeData darkTheme,
            StyleSheet lightSheet,
            StyleSheet darkSheet,
            TypographyConfig typographyConfig = null,
            Font defaultFont = null)
        {
            _lightTheme      = lightTheme;
            _darkTheme       = darkTheme;
            _lightSheet      = lightSheet;
            _darkSheet       = darkSheet;
            _typographyConfig = typographyConfig;
            _defaultFont     = defaultFont;
            _initialized     = true;

            if (_activeTheme == null && _lightTheme != null)
                _activeTheme = _lightTheme;

            SyncAllPanels();
        }

        /// <summary>
        /// Sets a new active theme, swaps USS stylesheets on all managed panels,
        /// and notifies all subscribers within the same frame.
        /// </summary>
        public static void SetTheme(ThemeData theme)
        {
            if (theme == null)
            {
                Debug.LogError("[UISystem] ThemeManager.SetTheme: null theme provided — ignoring.");
                return;
            }
            _activeTheme = theme;
            SyncAllPanels();

            if (OnThemeChanged != null)
            {
                foreach (var handler in OnThemeChanged.GetInvocationList())
                {
                    try { ((Action<ThemeData>)handler)(_activeTheme); }
                    catch (Exception ex) { Debug.LogException(ex); }
                }
            }
        }

        /// <summary>
        /// Toggles between the assigned light and dark theme variants.
        /// </summary>
        public static void ToggleLightDark()
        {
            if (_lightTheme == null || _darkTheme == null)
            {
                Debug.LogWarning("[UISystem] ThemeManager.ToggleLightDark: LightTheme or DarkTheme is not assigned.");
                return;
            }
            SetTheme(_activeTheme == _lightTheme ? _darkTheme : _lightTheme);
        }

        /// <summary>
        /// Register a UIDocument to receive theme USS sync. Call from Start()
        /// on any panel spawned at runtime.
        /// </summary>
        public static void RegisterPanel(UIDocument doc)
        {
            if (doc == null) return;

            // Check for duplicates
            foreach (var wr in _managedPanels)
            {
                if (wr.TryGetTarget(out var existing) && existing == doc)
                    return;
            }

            _managedPanels.Add(new WeakReference<UIDocument>(doc));

            if (_activeTheme != null)
                SyncToPanel(doc);
        }

        /// <summary>
        /// Unregister a UIDocument (e.g., before it is destroyed).
        /// </summary>
        public static void UnregisterPanel(UIDocument doc)
        {
            _managedPanels.RemoveAll(wr => !wr.TryGetTarget(out var d) || d == doc);
        }

        /// <summary>
        /// Swap the active theme USS stylesheet on a single panel's rootVisualElement.
        /// </summary>
        public static void SyncToPanel(UIDocument doc)
        {
            if (doc == null || _activeTheme == null) return;
            var root = doc.rootVisualElement;
            if (root == null) return;

            bool isLight = _activeTheme == _lightTheme;
            var toRemove = isLight ? _darkSheet  : _lightSheet;
            var toAdd    = isLight ? _lightSheet : _darkSheet;

            if (toRemove != null)
                root.styleSheets.Remove(toRemove);

            if (toAdd != null && !root.styleSheets.Contains(toAdd))
                root.styleSheets.Add(toAdd);

            // Unity 6.x UI Toolkit reads text rendering from `unityFontDefinition`. USS
            // `-unity-font: url(...)` writes into the legacy `unityFont` property only —
            // the new fontDefinition stays NotInter (the panel's runtime fallback) and the
            // rendered glyphs look like garbled vertical strokes. Pushing the Roboto Font
            // through `style.unityFontDefinition` makes both code paths agree.
            if (_defaultFont != null)
                root.style.unityFontDefinition = new StyleFontDefinition(_defaultFont);
        }

        // ------------------------------------------------------------------ //
        //  Private                                                            //
        // ------------------------------------------------------------------ //
        private static void SyncAllPanels()
        {
            // Remove dead references
            _managedPanels.RemoveAll(wr => !wr.TryGetTarget(out _));

            foreach (var wr in _managedPanels)
            {
                if (wr.TryGetTarget(out var doc))
                    SyncToPanel(doc);
            }
        }

        // ------------------------------------------------------------------ //
        //  Domain Reload Support (Editor)                                     //
        // ------------------------------------------------------------------ //
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void EditorDomainReload()
        {
            // Reset state on domain reload so ThemeBootstrapper re-initializes
            _initialized = false;
            _activeTheme = null;
            _lightTheme  = null;
            _darkTheme   = null;
            _lightSheet  = null;
            _darkSheet   = null;
            _typographyConfig = null;
            _managedPanels.Clear();
            OnThemeChanged = null;
        }
#endif
    }
}
