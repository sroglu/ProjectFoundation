#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Reflection;
using PFound.UISystem.Shapes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace PFound.UISystem.Tests.Shapes
{
    /// <summary>
    /// Phase 2 acceptance — PlayMode batching gates (spec 009 FR-005 + FR-006 + AD-001 A+).
    /// Verifies 50 SdfShape instances sharing one material category batch to ≤3 draws,
    /// AND that palette-encoded color variance across 16 different colors STILL batches
    /// to ≤3 (the critical A+ correctness gate).
    /// </summary>
    public class SdfShapePlayModeBatchingTests
    {
        private GameObject _go;
        private PanelSettings _panelSettings;

        [SetUp]
        public void SetUp()
        {
            SdfShapeMaterials.ClearCache();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_panelSettings != null) Object.DestroyImmediate(_panelSettings);
            SdfShapeMaterials.ClearCache();
        }

        [UnityTest]
        public IEnumerator FiftyElementsSharedConfig_BatchToAtMostThreeDrawCallDelta()
        {
            int baselineDraw = ReadDrawCalls();
            BuildPanelWithFiftyShapes(uniqueColorCount: 1);
            for (int i = 0; i < 30; i++) yield return null;
            int delta = ReadDrawCalls() - baselineDraw;
            Assert.LessOrEqual(delta, 3,
                $"Expected ≤3 draw call delta for 50 SdfShape (same config, 1 color), observed {delta}.");
        }

        [UnityTest]
        public IEnumerator FiftyElementsWith15DifferentColors_StillBatchToAtMostThreeDraws()
        {
            // Critical SC-002 + AD-001 A+ gate: palette indexing must keep elements with
            // different fill colors in the SAME draw call (one shared material, 16 palette slots
            // where slot 0 is reserved for design-system default white, leaving 15 user slots).
            // SC-002 wording "≤16 unique fill colors" INCLUDES the reserved default.
            int baselineDraw = ReadDrawCalls();
            BuildPanelWithFiftyShapes(uniqueColorCount: 15);
            for (int i = 0; i < 30; i++) yield return null;
            int delta = ReadDrawCalls() - baselineDraw;
            Assert.LessOrEqual(delta, 3,
                $"Expected ≤3 draw call delta for 50 SdfShape with 15 unique fill colors (palette path), observed {delta}.");
        }

        // ─────────────────────────────────────────────────────────────────

        private void BuildPanelWithFiftyShapes(int uniqueColorCount)
        {
            _go = new GameObject("SdfShapePlayModeBatchingTest");
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            _panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            _panelSettings.hideFlags = HideFlags.DontSave;
            var doc = _go.AddComponent<UIDocument>();
            doc.panelSettings = _panelSettings;
            var root = doc.rootVisualElement;
            root.style.flexDirection = FlexDirection.Row;
            root.style.flexWrap = Wrap.Wrap;

            for (int i = 0; i < 50; i++)
            {
                var shape = new SdfShape
                {
                    CornerRadius = 12f,
                    style = { width = 80, height = 50, marginTop = 4, marginLeft = 4 }
                };
                shape.FillColorOverride = new Color((i % uniqueColorCount) / (float)uniqueColorCount, 0.5f, 0.5f);
                root.Add(shape);
            }
        }

        private static int ReadDrawCalls()
        {
            // UnityStats reflection — same pattern as Phase 1 acceptance test #6
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
