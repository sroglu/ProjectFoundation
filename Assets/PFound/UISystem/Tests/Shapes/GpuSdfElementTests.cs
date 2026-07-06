#if UNITY_INCLUDE_TESTS
using PFound.UISystem.Shapes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace PFound.UISystem.Tests.Shapes
{
    /// <summary>
    /// EditMode smoke tests for <see cref="GpuSdfElement"/>. Validates the wrapper's
    /// two contracts: (a) automatic mesh trigger via backgroundColor and (b) round-trip
    /// of the Material property through StyleMaterialDefinition.
    /// </summary>
    public class GpuSdfElementTests
    {
        private Shader _shader;

        [SetUp]
        public void SetUp()
        {
            _shader = Shader.Find("UISystem/Shape");
            Assert.IsNotNull(_shader, "Shader 'UISystem/Shape' not found — Phase 1 dependency missing.");
        }

        [Test]
        public void Default_HasUssClassName()
        {
            var ve = new GpuSdfElement();
            CollectionAssert.Contains(ve.GetClasses(), GpuSdfElement.ussClassName);
        }

        [Test]
        public void Default_HasMeshTriggerBackgroundColor()
        {
            var ve = new GpuSdfElement();
            // resolvedStyle is empty until the element is attached to a panel; check style instead.
            var bg = ve.style.backgroundColor;
            // StyleColor.value is the Color; keyword check distinguishes "unset" vs assigned.
            Assert.AreEqual(StyleKeyword.Undefined, bg.keyword,
                "Expected backgroundColor to be a concrete value (mesh trigger), not a keyword.");
            Assert.AreEqual(Color.white, bg.value,
                "Expected backgroundColor = white (neutral mesh trigger).");
        }

        [Test]
        public void Material_AssignsToStyleUnityMaterial()
        {
            var mat = new Material(_shader) { hideFlags = HideFlags.DontSave };
            try
            {
                var ve = new GpuSdfElement { Material = mat };
                // style.unityMaterial returns StyleMaterialDefinition.
                var styleVal = ve.style.unityMaterial;
                Assert.AreEqual(StyleKeyword.Undefined, styleVal.keyword,
                    "Expected style.unityMaterial to hold a concrete value after assignment.");
                Assert.AreSame(mat, styleVal.value.material,
                    "Assigned Material did not round-trip via style.unityMaterial.");
            }
            finally
            {
                Object.DestroyImmediate(mat);
            }
        }

        [Test]
        public void Material_AssignsAndIsRetrievable()
        {
            var mat = new Material(_shader) { hideFlags = HideFlags.DontSave };
            try
            {
                var ve = new GpuSdfElement { Material = mat };
                Assert.AreSame(mat, ve.Material);
            }
            finally
            {
                Object.DestroyImmediate(mat);
            }
        }

        [Test]
        public void Material_NullSetter_ClearsInlineOverride()
        {
            var mat = new Material(_shader) { hideFlags = HideFlags.DontSave };
            try
            {
                var ve = new GpuSdfElement { Material = mat };
                ve.Material = null;
                var styleVal = ve.style.unityMaterial;
                Assert.AreEqual(StyleKeyword.Null, styleVal.keyword,
                    "Setting Material = null should clear the inline unityMaterial override (StyleKeyword.Null).");
            }
            finally
            {
                Object.DestroyImmediate(mat);
            }
        }

        [Test]
        public void Material_SameAssignment_DoesNotRedundantlyApply()
        {
            // Sanity: setting the same material twice should not change observable state.
            var mat = new Material(_shader) { hideFlags = HideFlags.DontSave };
            try
            {
                var ve = new GpuSdfElement { Material = mat };
                ve.Material = mat; // no-op
                Assert.AreSame(mat, ve.Material);
                Assert.AreSame(mat, ve.style.unityMaterial.value.material);
            }
            finally
            {
                Object.DestroyImmediate(mat);
            }
        }
    }
}
#endif
