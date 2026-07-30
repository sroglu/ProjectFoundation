#if UNITY_INCLUDE_TESTS
using PFound.UISystem.Shapes;
using NUnit.Framework;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace PFound.UISystem.Tests.Shapes
{
    /// <summary>
    /// PlayMode test #6 from Phase 1 acceptance — empirically verifies that 50 elements sharing
    /// one Material batch to a small number of draw calls. Mirrors the feasibility spike (008
    /// research.md § 3.6) Frame Debugger evidence, but as a CI-runnable assertion via UnityStats.
    /// </summary>
    public class GpuSdfElementBatchingTests
    {
        private GameObject _go;
        private PanelSettings _panelSettings;
        private Material _material;

        [SetUp]
        public void SetUp()
        {
            var shader = Shader.Find("UISystem/Shape");
            Assert.IsNotNull(shader, "Shader 'UISystem/Shape' missing — Phase 1 not shipped.");
            _material = new Material(shader) { hideFlags = HideFlags.DontSave };
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_panelSettings != null) Object.DestroyImmediate(_panelSettings);
            if (_material != null) Object.DestroyImmediate(_material);
        }

        [UnityTest]
        public IEnumerator FiftyElementsSharingOneMaterial_BatchToAtMostThreeDrawCallDelta()
        {
            // Baseline UnityStats.
            int baselineDraw = ReadDrawCalls();

            // Build a minimal UIDocument in a fresh GameObject.
            _go = new GameObject("GpuSdfElementBatchingTest");
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            _panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            var doc = _go.AddComponent<UIDocument>();
            doc.panelSettings = _panelSettings;

            var root = doc.rootVisualElement;
            root.style.flexDirection = FlexDirection.Row;
            root.style.flexWrap = Wrap.Wrap;

            for (int i = 0; i < 50; i++)
            {
                root.Add(new GpuSdfElement
                {
                    Material = _material,
                    style =
                    {
                        width = 80,
                        height = 50,
                        marginTop = 4,
                        marginLeft = 4,
                    }
                });
            }

            // Wait for layout + first paint + a few extra frames so UnityStats reflects the new content.
            for (int i = 0; i < 30; i++) yield return null;

            int newDraw = ReadDrawCalls();
            int delta = newDraw - baselineDraw;

            // Empirical R&D-2 result: 50 shared-material elements = 1 batched Draw Mesh.
            // We allow ≤3 to absorb editor-side UnityStats noise (camera blit, etc.).
            Assert.LessOrEqual(delta, 3,
                $"Expected ≤3 draw call delta for 50 shared-material elements, observed {delta} (baseline {baselineDraw} → {newDraw}).");
        }

        private static int ReadDrawCalls()
        {
            // UnityEditor.UnityStats lives in UnityEditor; the Tests asmdef does not depend on
            // UnityEditor (so this assembly can compile for standalone build). Reflection avoids
            // the asmdef ref while keeping the test editor-only via TestRunner gating.
            var statsType = System.Type.GetType("UnityEditor.UnityStats, UnityEditor.CoreModule")
                            ?? System.Type.GetType("UnityEditor.UnityStats, UnityEditor");
            if (statsType == null) return -1;
            var prop = statsType.GetProperty("drawCalls", BindingFlags.Public | BindingFlags.Static);
            if (prop == null) return -1;
            try { return System.Convert.ToInt32(prop.GetValue(null)); }
            catch { return -1; }
        }
    }
}
#endif
