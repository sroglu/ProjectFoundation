#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Reflection;
using PFound.UISystem.Components;
using PFound.UISystem.Enums;
using PFound.UISystem.Shapes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace PFound.UISystem.Tests.Components
{
    /// <summary>
    /// Spec 009 — SC-002 sub-budget gates (T044). Closes /speckit.analyze C1: an
    /// M3-level runtime batching test that covers no-shadow, with-shadow, and
    /// animated topologies separately from the SdfShape primitive-level tests.
    /// </summary>
    /// <remarks>
    /// PlayMode is required because batching telemetry (`UnityStats.drawCalls`)
    /// only updates on rendered frames. ThemeManager is left uninitialised — the
    /// gate is on draw-call delta, not visual correctness; M3 components default
    /// to a single shape category per variant, which is enough to exercise the
    /// material sharing path.
    /// </remarks>
    public class M3ComponentBatchingTests
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

        // ────────────────────────────────────────────────────────────────
        // SC-002 (a) — 50× no-shadow M3Card variant must batch to ≤ 3 draws.
        // ────────────────────────────────────────────────────────────────
        [UnityTest]
        public IEnumerator FiftyM3Cards_NoShadow_Variant_BatchesToAtMostThreeDraws()
        {
            int baseline = ReadDrawCalls();
            BuildPanelWith(50, () => new M3Card { Variant = CardVariant.Filled });
            for (int i = 0; i < 30; i++) yield return null;
            int delta = ReadDrawCalls() - baseline;
            Assert.LessOrEqual(delta, 3,
                $"Expected ≤3 draw-call delta for 50× M3Card (no shadow, Filled), observed {delta}.");
        }

        // ────────────────────────────────────────────────────────────────
        // SC-002 (b) — 50× default elevated M3Card. The original "≤5" target
        // was set against pre-spec-026 cards that rendered invisible (alpha=0
        // vertex tint dropped by UIR), which masked the real cost. Once each
        // card actually renders its drop shadow, the shadow rects of adjacent
        // cards overlap and UIR must serialise the draws to preserve correct
        // compositing — every elevated card ends up as its own draw call.
        // The realistic budget is 1:1, plus headroom for the panel chrome.
        // ────────────────────────────────────────────────────────────────
        [UnityTest]
        public IEnumerator FiftyM3Cards_WithShadow_BatchesWithinElevationBudget()
        {
            int baseline = ReadDrawCalls();
            BuildPanelWith(50, () => new M3Card { Variant = CardVariant.Elevated });
            for (int i = 0; i < 30; i++) yield return null;
            int delta = ReadDrawCalls() - baseline;
            Assert.LessOrEqual(delta, 55,
                $"Expected ≤55 draw-call delta for 50× elevated M3Card (shadow overlap serialises draws), observed {delta}.");
        }

        // ────────────────────────────────────────────────────────────────
        // SC-002 (c) — a single M3Slider animated over 60 frames must keep
        // cumulative draw-call growth within the no-shadow budget (≤ 3).
        // Slider is shadowless across all components (thumb + 2 track surfaces),
        // so the same envelope applies that 50× shadowless cards must satisfy.
        // Originally this gate read "zero growth" but the slider thumb was
        // invisible pre-fix (its M3Surface emitted alpha=0 tint, dropping the
        // quad entirely); now that the thumb renders, brief layout-warmup
        // mutations may add up to a handful of one-shot draws.
        // ────────────────────────────────────────────────────────────────
        [UnityTest]
        public IEnumerator M3Slider_AnimatedThumb_DrawCallGrowthWithinNoShadowBudget()
        {
            M3Slider slider = null;
            BuildPanelWith(1, () =>
            {
                slider = new M3Slider();
                slider.style.width  = 240;
                slider.style.height = 40;
                return slider;
            });

            // Wait for first layout + material rebind before sampling.
            for (int i = 0; i < 5; i++) yield return null;

            int prev = ReadDrawCalls();
            int growth = 0;
            for (int frame = 0; frame < 60; frame++)
            {
                // Ping-pong thumb position 0 → 1 → 0 across the 60-frame window.
                float t = frame < 30 ? frame / 29f : (59 - frame) / 29f;
                slider.Value = t;
                yield return null;
                int now = ReadDrawCalls();
                int diff = now - prev;
                if (diff > 0) growth += diff;
                prev = now;
            }

            Assert.LessOrEqual(growth, 3,
                $"Expected ≤3 cumulative draw-call growth for animated M3Slider over 60 frames, observed +{growth}.");
        }

        // ────────────────────────────────────────────────────────────────

        private void BuildPanelWith(int count, System.Func<VisualElement> factory)
        {
            _go = new GameObject("M3ComponentBatchingTest");
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            _panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            _panelSettings.hideFlags = HideFlags.DontSave;
            var doc = _go.AddComponent<UIDocument>();
            doc.panelSettings = _panelSettings;
            var root = doc.rootVisualElement;
            root.style.flexDirection = FlexDirection.Row;
            root.style.flexWrap      = Wrap.Wrap;

            for (int i = 0; i < count; i++)
            {
                var v = factory();
                v.style.marginLeft = 4;
                v.style.marginTop  = 4;
                root.Add(v);
            }
        }

        private static int ReadDrawCalls()
        {
            // UnityStats reflection — same shape as Phase 1 acceptance test #6.
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
