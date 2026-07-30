#if UNITY_INCLUDE_TESTS
using PFound.UISystem.Components.M3;
using PFound.UISystem.Shapes;
using NUnit.Framework;
using UnityEngine;

namespace PFound.UISystem.Tests.Shapes
{
    /// <summary>
    /// Phase 2 acceptance — M3Surface 7-property layer (T012).
    /// Verifies inheritance from SdfShape + M3-specific properties + tint repacking.
    /// </summary>
    public class M3SurfaceLayerTests
    {
        [SetUp]
        public void SetUp() { SdfShapeMaterials.ClearCache(); }

        [TearDown]
        public void TearDown() { SdfShapeMaterials.ClearCache(); }

        [Test]
        public void M3Surface_IsA_SdfShape()
        {
            var m = new M3Surface();
            Assert.IsInstanceOf<SdfShape>(m);
        }

        [Test]
        public void M3Surface_InheritsSdfShapeProperties()
        {
            var m = new M3Surface { CornerRadius = 16f, OutlineThickness = 2f };
            Assert.AreEqual(16f, m.CornerRadius);
            Assert.AreEqual(2f, m.OutlineThickness);
        }

        [Test]
        public void TonalOverlayOpacity_DefaultZero_ClampsToZeroOne()
        {
            var m = new M3Surface();
            Assert.AreEqual(0f, m.TonalOverlayOpacity);
            m.TonalOverlayOpacity = -0.5f;
            Assert.AreEqual(0f, m.TonalOverlayOpacity);
            m.TonalOverlayOpacity = 1.5f;
            Assert.AreEqual(1f, m.TonalOverlayOpacity);
            m.TonalOverlayOpacity = 0.42f;
            Assert.AreEqual(0.42f, m.TonalOverlayOpacity, 0.001f);
        }

        [Test]
        public void StateOverlayOpacity_DefaultZero_ClampsToZeroOne()
        {
            var m = new M3Surface();
            Assert.AreEqual(0f, m.StateOverlayOpacity);
            m.StateOverlayOpacity = 2f;
            Assert.AreEqual(1f, m.StateOverlayOpacity);
        }

        [Test]
        public void OverlayColors_RoundTrip()
        {
            var stateColor = new Color(0.1f, 0.2f, 0.3f);
            var tonalColor = new Color(0.4f, 0.5f, 0.6f);
            var m = new M3Surface { StateOverlayColor = stateColor, TonalOverlayColor = tonalColor };
            Assert.AreEqual(stateColor, m.StateOverlayColor);
            Assert.AreEqual(tonalColor, m.TonalOverlayColor);
        }

        [Test]
        public void Ripple_PropertiesRoundTrip()
        {
            var m = new M3Surface { RippleCenter = new Vector2(0.3f, 0.7f), RippleRadius = 25f, RippleAlpha = 0.4f };
            Assert.AreEqual(new Vector2(0.3f, 0.7f), m.RippleCenter);
            Assert.AreEqual(25f, m.RippleRadius);
            Assert.AreEqual(0.4f, m.RippleAlpha);
        }

        [Test]
        public void TintEncoding_StatePacksIntoRNibble_TonalIntoB_AlphaForcedTo255()
        {
            var m = new M3Surface { StateOverlayOpacity = 0.5f, TonalOverlayOpacity = 0.25f };
            var bg = m.style.backgroundColor.value;
            // SPEC 026 anti-corruption encoding (matches UIShape.shader decode):
            //   byte 0 (R) = (fillIdx << 4) | stateOp4  — state opacity quantized to 4 bits (0-15);
            //                the shader reads stateOpacity = (rawR & 0xF) / 15.
            //   byte 1 (G) = 0 (reserved — a non-zero byte 1 corrupts the UIR tint path).
            //   byte 2 (B) = tonalOpacity * 255 — shader reads tonal from IN.color.b directly.
            //   byte 3 (A) = 255 (forced) so UIR emits a quad even when both overlays are 0.
            int r = Mathf.RoundToInt(bg.r * 255f);
            int g = Mathf.RoundToInt(bg.g * 255f);
            int b = Mathf.RoundToInt(bg.b * 255f);
            int a = Mathf.RoundToInt(bg.a * 255f);
            // state 0.5 → RoundToInt(0.5 * 15) = 8 in the low nibble; fill idx 0 in the high nibble.
            Assert.AreEqual(8, r & 0xF, "State opacity 0.5 should quantize to nibble 8 in tint.r");
            Assert.AreEqual(0, r >> 4,  "Default fill palette index (0) lives in the high nibble of tint.r");
            Assert.AreEqual(0, g,       "Tint byte 1 (G) must stay 0 — reserved by the anti-corruption encoding");
            Assert.AreEqual(64, b, 2,   "Tonal opacity 0.25 should encode to tint.b ≈ 64");
            Assert.AreEqual(255, a,     "Tint.a must be forced to 255 so UIR emits the quad");
        }
    }
}
#endif
