using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using PFound.UISystem;
using PFound.UISystem.Components;
using PFound.UISystem.Core;

namespace PFound.UISystem.Editor.MenuItems
{
    /// <summary>
    /// Spec 009 Phase 3 / T016 — captures per-component PNG baselines for each M3 component
    /// in its default state. Saved to <c>Assets/GameSpecific/UISystem/Tests/VisualBaselines/</c>
    /// (consumer-side per Constitution Principle III). Baselines are the pre-migration reference
    /// for spec 009 Phase 3.2 per-component visual parity diff tests.
    /// </summary>
    /// <remarks>
    /// <b>Approach</b>: programmatic per-component isolation render via
    /// <see cref="PanelSettings.targetTexture"/> + a temporary <see cref="MonoBehaviour"/>
    /// coroutine that yields <see cref="WaitForEndOfFrame"/> so UIR actually paints into the
    /// RT before pixels are read. <c>Thread.Sleep</c> alone doesn't advance Unity's frame loop —
    /// the render pipeline never executes. The coroutine pattern is the only correct path.
    /// <para>
    /// <b>Requires PlayMode</b> — UI Toolkit runtime panels don't render in EditMode. The menu
    /// item enters PlayMode and uses <see cref="SessionState"/> + <see cref="InitializeOnLoadAttribute"/>
    /// to bridge the domain reload.
    /// </para>
    /// </remarks>
    [InitializeOnLoad]
    internal static class M3VisualBaselineCapture
    {
        public const string BaselineDir = "Assets/GameSpecific/UISystem/Tests/VisualBaselines";
        private const string PendingKey = "UISystem.M3VisualBaseline.Pending";
        private const int CaptureWidth = 640;
        private const int CaptureHeight = 480;
        private const int WarmupFrames = 15;

        static M3VisualBaselineCapture()
        {
            if (SessionState.GetBool(PendingKey, false))
            {
                EditorApplication.update += SpawnDispatcherAfterPlayModeReady;
            }
        }

        [MenuItem("Tools/UISystem/Capture M3 Visual Baselines")]
        public static void Run()
        {
            if (!Directory.Exists(BaselineDir))
            {
                Directory.CreateDirectory(BaselineDir);
                AssetDatabase.Refresh();
            }
            if (!Application.isPlaying)
            {
                SessionState.SetBool(PendingKey, true);
                Debug.Log("[M3VisualBaselineCapture] Entering PlayMode to capture baselines…");
                EditorApplication.EnterPlaymode();
                return;
            }
            SpawnDispatcher();
        }

        private static void SpawnDispatcherAfterPlayModeReady()
        {
            if (!Application.isPlaying) return;
            EditorApplication.update -= SpawnDispatcherAfterPlayModeReady;
            EditorApplication.delayCall += SpawnDispatcher;
        }

        private static void SpawnDispatcher()
        {
            var go = new GameObject("M3VisualBaselineDispatcher");
            var dispatcher = go.AddComponent<CaptureDispatcher>();
            dispatcher.Components = BuildComponentList();
            dispatcher.WarmupFrames = WarmupFrames;
            dispatcher.Width = CaptureWidth;
            dispatcher.Height = CaptureHeight;
            dispatcher.OnAllCaptured = () =>
            {
                SessionState.EraseBool(PendingKey);
                AssetDatabase.Refresh();
                EditorApplication.ExitPlaymode();
            };
        }

        private static List<(string name, Func<VisualElement> factory)> BuildComponentList()
        {
            return new List<(string, Func<VisualElement>)>
            {
                ("M3Card",          () => new M3Card          { style = { width = 280, height = 180 } }),
                ("M3Button",        () => new M3Button        { Text = "Button", style = { width = 180, height = 48 } }),
                ("M3Chip",          () => new M3Chip          { Text = "Chip", style = { width = 120, height = 32 } }),
                ("M3FAB",           () => new M3FAB           { Text = "FAB", style = { width = 56, height = 56 } }),
                ("M3Checkbox",      () => new M3Checkbox      { style = { width = 24, height = 24 } }),
                ("M3RadioButton",   () => new M3RadioButton   { Text = "Option", style = { width = 24, height = 24 } }),
                ("M3Toggle",        () => new M3Toggle        { style = { width = 52, height = 32 } }),
                ("M3TextField",     () => new M3TextField     { Label = "Label", style = { width = 280, height = 56 } }),
                ("M3TopAppBar",     () => new M3TopAppBar     { Headline = "Title", style = { width = 560, height = 64 } }),
                ("M3BottomAppBar",  () => new M3BottomAppBar  { style = { width = 560, height = 80 } }),
                ("M3Menu",          () => new M3Menu          { style = { width = 240, height = 160 } }),
                ("M3NavigationItem",() => new M3NavigationItem{ Label = "Item", style = { width = 80, height = 64 } }),
                ("M3Dialog",        () => new M3Dialog        { Headline = "Dialog", Body = "Body text", style = { width = 320, height = 200 } }),
                ("M3Slider",        () => new M3Slider        { Value = 0.5f, style = { width = 280, height = 48 } }),
            };
        }

        /// <summary>
        /// Runtime coroutine-based capture orchestrator. Iterates components, swaps panel content,
        /// waits N WaitForEndOfFrame ticks for UIR to paint into <see cref="PanelSettings.targetTexture"/>,
        /// reads RT pixels and saves PNG.
        /// </summary>
        private class CaptureDispatcher : MonoBehaviour
        {
            public List<(string name, Func<VisualElement> factory)> Components;
            public int WarmupFrames;
            public int Width;
            public int Height;
            public Action OnAllCaptured;

            private void Start() => StartCoroutine(Run());

            private IEnumerator Run()
            {
                Debug.Log($"[M3VisualBaselineCapture] Capturing {Components.Count} components → {BaselineDir}");

                // One panel + RT reused across all components — replace root content per iteration.
                var rt = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32) { hideFlags = HideFlags.DontSave };
                var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                panelSettings.hideFlags = HideFlags.DontSave;
                panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
                panelSettings.referenceResolution = new Vector2Int(Width, Height);
                panelSettings.targetTexture = rt;
                panelSettings.clearColor = true;
                panelSettings.colorClearValue = new Color(0.1f, 0.1f, 0.1f, 1f);

                // CRITICAL: UIDocument refuses to render without a Theme Style Sheet (TSS).
                // Without this, all elements remain blank — even simple shapes. The TSS lives
                // at UISystem/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss; reflection
                // is used because ThemeStyleSheet type is in UnityEditor namespace and would
                // require an editor-only reference in this asmdef (we already have one).
                var tssPath = "Assets/PFound/UISystem/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss";
                var tssType = System.Type.GetType("UnityEngine.UIElements.ThemeStyleSheet, UnityEngine.UIElementsModule");
                if (tssType != null)
                {
                    var tss = AssetDatabase.LoadAssetAtPath(tssPath, tssType);
                    if (tss != null)
                    {
                        var prop = typeof(PanelSettings).GetProperty("themeStyleSheet",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        prop?.SetValue(panelSettings, tss);
                        Debug.Log($"[M3VisualBaselineCapture] ThemeStyleSheet loaded: {tssPath}");
                    }
                    else Debug.LogWarning($"[M3VisualBaselineCapture] TSS asset not found at {tssPath}");
                }
                else Debug.LogWarning("[M3VisualBaselineCapture] ThemeStyleSheet type not found via reflection");

                var doc = gameObject.AddComponent<UIDocument>();
                doc.panelSettings = panelSettings;

                // Initialize ThemeManager so M3 component theme-color bindings resolve.
                // Without this, color-bound components (M3Card, M3Button, M3Dialog, etc.)
                // render with default/missing colors → blank capture.
                var lightTheme  = Resources.Load<ThemeData>("UISystem/DefaultLight");
                var darkTheme   = Resources.Load<ThemeData>("UISystem/DefaultDark");
                var lightSheet  = Resources.Load<StyleSheet>("UISystem/light");
                var darkSheet   = Resources.Load<StyleSheet>("UISystem/dark");
                var typoConfig  = Resources.Load<TypographyConfig>("UISystem/DefaultTypography");
                if (lightTheme != null && darkTheme != null && lightSheet != null && darkSheet != null)
                {
                    ThemeManager.Initialize(lightTheme, darkTheme, lightSheet, darkSheet, typoConfig);
                    ThemeManager.RegisterPanel(doc);
                    ThemeManager.SyncToPanel(doc);
                    Debug.Log($"[M3VisualBaselineCapture] ThemeManager initialized (light={lightTheme.name}, dark={darkTheme.name})");
                }
                else
                {
                    Debug.LogWarning($"[M3VisualBaselineCapture] Theme assets missing from Resources/UISystem/ — captures may be blank for theme-bound components (light={lightTheme}, dark={darkTheme}, lightSheet={lightSheet}, darkSheet={darkSheet}, typo={typoConfig})");
                }

                foreach (var (componentName, factory) in Components)
                {
                    var root = doc.rootVisualElement;
                    root.Clear();
                    VisualElement element;
                    try { element = factory(); }
                    catch (Exception ex)
                    {
                        Debug.LogError($"  ✗ {componentName} factory threw: {ex.Message}");
                        continue;
                    }
                    if (element == null) { Debug.LogWarning($"  ✗ {componentName}: factory returned null"); continue; }

                    // Absolute positioning centered in canvas — bypasses flex layout pass which
                    // sometimes hasn't completed by frame N for complex M3 components. element's
                    // style.width/height are factory-set; we use them to derive center offset.
                    var width = element.resolvedStyle.width;
                    var height = element.resolvedStyle.height;
                    if (float.IsNaN(width)) width = element.style.width.value.value;
                    if (float.IsNaN(height)) height = element.style.height.value.value;
                    element.style.position = Position.Absolute;
                    element.style.left = (Width - (int)width) / 2f;
                    element.style.top = (Height - (int)height) / 2f;
                    root.Add(element);

                    // Yield enough frames for UIR to (a) layout, (b) build mesh, (c) paint into RT.
                    for (int i = 0; i < WarmupFrames; i++) yield return new WaitForEndOfFrame();

                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
                    tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                    tex.Apply();
                    RenderTexture.active = prev;

                    try
                    {
                        var bytes = tex.EncodeToPNG();
                        File.WriteAllBytes($"{BaselineDir}/{componentName}.png", bytes);
                        Debug.Log($"  ✓ {componentName}.png ({bytes.Length} bytes)");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"  ✗ {componentName} save failed: {ex.Message}");
                    }
                    UnityEngine.Object.DestroyImmediate(tex);
                }

                UnityEngine.Object.DestroyImmediate(panelSettings);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                Debug.Log("[M3VisualBaselineCapture] Done.");
                OnAllCaptured?.Invoke();
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }
    }
}
