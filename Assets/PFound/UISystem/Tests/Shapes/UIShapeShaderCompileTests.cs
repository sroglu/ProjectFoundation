#if UNITY_INCLUDE_TESTS
using PFound.UISystem.Shapes;
using NUnit.Framework;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PFound.UISystem.Tests.Shapes
{
    /// <summary>
    /// EditMode tests for the UISystem/Shape shader. Validates compile, keyword surface,
    /// inspector contract, and the basic API guarantees observed during the 008 feasibility
    /// spike (research.md § 3.x).
    /// </summary>
    public class UIShapeShaderCompileTests
    {
        private const string ShaderName = "UISystem/Shape";

        private Shader _shader;

        [SetUp]
        public void SetUp()
        {
            _shader = Shader.Find(ShaderName);
        }

        // Test 1 — Shader_Resolves
        [Test]
        public void Shader_Resolves()
        {
            Assert.IsNotNull(_shader, $"Shader '{ShaderName}' not found via Shader.Find.");
            Assert.AreEqual(ShaderName, _shader.name);
        }

        // Test 2 — Shader_ZeroCompileMessages (current platform)
        [Test]
        public void Shader_ZeroCompileMessages()
        {
            Assert.IsNotNull(_shader);
            int messages = ShaderUtil.GetShaderMessageCount(_shader);
            if (messages > 0)
            {
                var msgs = ShaderUtil.GetShaderMessages(_shader);
                foreach (var m in msgs)
                {
                    UnityEngine.Debug.Log($"[{m.severity}] {m.platform} {m.message} (line {m.line})");
                }
            }
            Assert.AreEqual(0, messages, "Shader compile produced messages.");
        }

        // Test 3 — Material_KeywordSync_OutlineToggle: toggling _OutlineEnable property must
        // mirror EFFECT_OUTLINE_ON keyword (via SetEffectEnabled helper that the Inspector uses).
        [Test]
        public void Material_KeywordSync_OutlineToggle()
        {
            Assert.IsNotNull(_shader);
            var mat = new Material(_shader);
            try
            {
                // Initial state — outline OFF
                Assert.IsFalse(mat.IsKeywordEnabled(UIShapeShaderKeywords.EffectOutlineOn));
                Assert.AreEqual(0f, mat.GetFloat(UIShapeMaterialProperties.OutlineEnable));

                // Helper sets both in lockstep
                UIShapeMaterialHelpers.SetEffectEnabled(
                    mat, UIShapeMaterialProperties.OutlineEnable,
                    UIShapeShaderKeywords.EffectOutlineOn, true);

                Assert.AreEqual(1f, mat.GetFloat(UIShapeMaterialProperties.OutlineEnable));
                Assert.IsTrue(mat.IsKeywordEnabled(UIShapeShaderKeywords.EffectOutlineOn));

                // Toggle back
                UIShapeMaterialHelpers.SetEffectEnabled(
                    mat, UIShapeMaterialProperties.OutlineEnable,
                    UIShapeShaderKeywords.EffectOutlineOn, false);

                Assert.AreEqual(0f, mat.GetFloat(UIShapeMaterialProperties.OutlineEnable));
                Assert.IsFalse(mat.IsKeywordEnabled(UIShapeShaderKeywords.EffectOutlineOn));
            }
            finally
            {
                Object.DestroyImmediate(mat);
            }
        }

        // Test 4 — KeywordSpace_HasExpectedKeywords:
        // 6 effect (shader_feature_local) + 2 sub-mode (shader_feature_local) + 4 shape (multi_compile_local)
        // = 12 declared shape/effect keywords (plus stereo/XR keywords auto-added by URP).
        [Test]
        public void KeywordSpace_HasExpectedKeywords()
        {
            Assert.IsNotNull(_shader);
            var allKeywordNames = _shader.keywordSpace.keywords.Select(k => k.name).ToArray();

            string[] expected =
            {
                UIShapeShaderKeywords.ShapeTypeRect,
                UIShapeShaderKeywords.ShapeTypeRoundedRect,
                UIShapeShaderKeywords.ShapeTypeCapsule,
                UIShapeShaderKeywords.ShapeTypeEllipse,
                UIShapeShaderKeywords.EffectGradientOn,
                UIShapeShaderKeywords.EffectOutlineOn,
                UIShapeShaderKeywords.EffectBandingOn,
                UIShapeShaderKeywords.EffectNoiseOn,
                UIShapeShaderKeywords.EffectDotsOn,
                UIShapeShaderKeywords.EffectShadowOn,
                UIShapeShaderKeywords.GradientModeRadial,
                UIShapeShaderKeywords.NoiseModeWorley,
            };

            foreach (var name in expected)
            {
                CollectionAssert.Contains(allKeywordNames, name,
                    $"Expected shader keyword '{name}' not found in keywordSpace.");
            }
        }

        // Test 5 — MaterialDefinition_AcceptsThisMaterial:
        // IStyle.unityMaterial round-trips the material — empirical proof from feasibility R&D-1/R&D-5.
        [Test]
        public void MaterialDefinition_AcceptsThisMaterial()
        {
            Assert.IsNotNull(_shader);
            var mat = new Material(_shader);
            try
            {
                var ve = new UnityEngine.UIElements.VisualElement();
                ve.style.unityMaterial = new UnityEngine.UIElements.StyleMaterialDefinition(mat);

                var resolved = ve.resolvedStyle.unityMaterial.material;
                Assert.AreSame(mat, resolved,
                    "resolvedStyle.unityMaterial.material did not round-trip the assigned material.");
            }
            finally
            {
                Object.DestroyImmediate(mat);
            }
        }

        // Test 6 — CompositionString_OrderIsStable:
        // EffectMask → composition string follows documented shadow → fill → ... → outline order.
        [Test]
        public void CompositionString_OrderIsStable()
        {
            // No effects → just fill
            Assert.AreEqual("fill", UIShapeEffectComposition.GetCompositionString(EffectMask.None));

            // All effects → full order
            const EffectMask all = EffectMask.Shadow | EffectMask.Gradient | EffectMask.Banding
                                   | EffectMask.Noise | EffectMask.Dots | EffectMask.Outline;
            Assert.AreEqual(
                "shadow → fill → gradient → banding → noise → dots → outline",
                UIShapeEffectComposition.GetCompositionString(all));

            // Shadow + outline only
            Assert.AreEqual(
                "shadow → fill → outline",
                UIShapeEffectComposition.GetCompositionString(EffectMask.Shadow | EffectMask.Outline));
        }
    }
}
#endif
