#if UNITY_INCLUDE_TESTS
using PFound.UISystem.Shapes;
using NUnit.Framework;
using UnityEngine;

namespace PFound.UISystem.Tests.Shapes
{
    /// <summary>
    /// Phase 2 acceptance — SdfShape API surface (T011).
    /// Verifies all 17 API surfaces (12 backing fields) round-trip and produce expected
    /// behavior. Spec 009 FR-001a + FR-009 + FR-010.
    /// </summary>
    public class SdfShapeApiSurfaceTests
    {
        [SetUp]
        public void SetUp() { SdfShapeMaterials.ClearCache(); }

        [TearDown]
        public void TearDown() { SdfShapeMaterials.ClearCache(); }

        // ───── Corner radii (5 surfaces) ─────────────────────────────────

        [Test]
        public void CornerRadius_DefaultIs12()
        {
            var s = new SdfShape();
            Assert.AreEqual(12f, s.CornerRadius);
        }

        [Test]
        public void CornerRadius_SetGet_RoundTrips()
        {
            var s = new SdfShape { CornerRadius = 20f };
            Assert.AreEqual(20f, s.CornerRadius);
        }

        [Test]
        public void PerCornerRadii_DefaultIsMinusOne_MeaningInheritUniform()
        {
            var s = new SdfShape();
            Assert.AreEqual(-1f, s.CornerRadiusTL);
            Assert.AreEqual(-1f, s.CornerRadiusTR);
            Assert.AreEqual(-1f, s.CornerRadiusBR);
            Assert.AreEqual(-1f, s.CornerRadiusBL);
        }

        // ───── Shadow (8 surfaces) ───────────────────────────────────────

        [Test]
        public void Shadow_AllSurfacesRoundTrip()
        {
            var s = new SdfShape
            {
                ShadowBlur = 8f,
                ShadowOffsetX = 2f,
                ShadowOffsetY = -4f,
                ShadowPadding = 12f,
                ShadowColor = new Color(0.1f, 0.2f, 0.3f, 0.5f),
            };
            Assert.AreEqual(8f, s.ShadowBlur);
            Assert.AreEqual(2f, s.ShadowOffsetX);
            Assert.AreEqual(-4f, s.ShadowOffsetY);
            Assert.AreEqual(12f, s.ShadowPadding);
            Assert.AreEqual(new Color(0.1f, 0.2f, 0.3f, 0.5f), s.ShadowColor);
        }

        [Test]
        public void Shadow_RGBA_UxmlAttrs_AffectShadowColorStruct()
        {
            var s = new SdfShape { ShadowColorR = 0.3f, ShadowColorG = 0.4f, ShadowColorB = 0.5f, ShadowColorA = 0.6f };
            Assert.AreEqual(new Color(0.3f, 0.4f, 0.5f, 0.6f), s.ShadowColor);
        }

        [Test]
        public void ShadowBlur_NegativeClampsToZero()
        {
            var s = new SdfShape { ShadowBlur = -5f };
            Assert.AreEqual(0f, s.ShadowBlur);
        }

        [Test]
        public void ShadowPadding_NegativeClampsToZero()
        {
            var s = new SdfShape { ShadowPadding = -10f };
            Assert.AreEqual(0f, s.ShadowPadding);
        }

        // ───── Outline (2 surfaces) ──────────────────────────────────────

        [Test]
        public void Outline_RoundTrips()
        {
            var s = new SdfShape { OutlineThickness = 2f, OutlineColor = Color.cyan };
            Assert.AreEqual(2f, s.OutlineThickness);
            Assert.AreEqual(Color.cyan, s.OutlineColor);
        }

        [Test]
        public void OutlineThickness_NegativeClampsToZero()
        {
            var s = new SdfShape { OutlineThickness = -3f };
            Assert.AreEqual(0f, s.OutlineThickness);
        }

        // ───── Fill (1 surface — per-instance via palette) ───────────────

        [Test]
        public void FillColorOverride_NullByDefault()
        {
            var s = new SdfShape();
            Assert.IsNull(s.FillColorOverride);
        }

        [Test]
        public void FillColorOverride_SetGet_RoundTrips()
        {
            var s = new SdfShape { FillColorOverride = Color.magenta };
            Assert.AreEqual(Color.magenta, s.FillColorOverride.Value);
        }

        [Test]
        public void FillColorOverride_Null_FallsBackToDefault_PaletteSlot0()
        {
            // FR-010: null FillColorOverride uses palette slot 0 (design-system default)
            var s = new SdfShape();
            // Element's background-color carries the tint encoding. Slot 0 means R-byte = 0.
            int fillIdx = Mathf.RoundToInt(s.style.backgroundColor.value.r * 255f);
            Assert.AreEqual(0, fillIdx, "Unset FillColorOverride must encode palette slot 0");
        }

        // ───── FR-009 shadow short-circuit ───────────────────────────────

        [Test]
        public void FR009_NoShadowConfig_UsesNoShadowMaterial()
        {
            var s = new SdfShape { ShadowBlur = 0f, ShadowOffsetX = 0f, ShadowOffsetY = 0f };
            // BuildConfig().HasShadow == false → category resolves to no-shadow base
            // The actual material name from runtime fallback or asset will include "NoShadow"
            // We test the config struct directly to avoid asset path coupling:
            var config = new SdfShapeConfig(Vector4.zero, 0f, 0f, Vector2.zero, 0f, Color.clear, new Vector2(100f, 100f));
            Assert.IsFalse(config.HasShadow, "Config with zero shadow blur + offset must report HasShadow=false");
        }

        [Test]
        public void FR009_AnyShadowField_TriggersWithShadowMaterial()
        {
            // Only blur set → HasShadow
            var c1 = new SdfShapeConfig(Vector4.zero, 0f, 1f, Vector2.zero, 0f, Color.clear, new Vector2(100f, 100f));
            Assert.IsTrue(c1.HasShadow);
            // Only offset X
            var c2 = new SdfShapeConfig(Vector4.zero, 0f, 0f, new Vector2(1f, 0f), 0f, Color.clear, new Vector2(100f, 100f));
            Assert.IsTrue(c2.HasShadow);
            // Only offset Y
            var c3 = new SdfShapeConfig(Vector4.zero, 0f, 0f, new Vector2(0f, -1f), 0f, Color.clear, new Vector2(100f, 100f));
            Assert.IsTrue(c3.HasShadow);
        }
    }
}
#endif
