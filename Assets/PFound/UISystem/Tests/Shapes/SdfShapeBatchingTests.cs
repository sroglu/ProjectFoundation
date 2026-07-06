#if UNITY_INCLUDE_TESTS
using PFound.UISystem.Shapes;
using NUnit.Framework;
using UnityEngine;

namespace PFound.UISystem.Tests.Shapes
{
    /// <summary>
    /// Phase 2 acceptance — EditMode-only assertions for SdfShape + palette infrastructure.
    /// The PlayMode batching gate (50× shared material, 50× 16-color palette) lives in
    /// <c>PFound.UISystem.Tests.PlayMode.SdfShapePlayModeBatchingTests</c> (separate
    /// asmdef so PlayMode TestRunner can discover it).
    /// </summary>
    public class SdfShapeBatchingTests
    {
        [SetUp]
        public void SetUp() { SdfShapeMaterials.ClearCache(); }

        [TearDown]
        public void TearDown() { SdfShapeMaterials.ClearCache(); }

        [Test]
        public void PaletteOverflow_ThrowsUISystemPaletteOverflowException()
        {
            // SC-002 fail-loud rule: exceeding MaxSlots must throw, not silently quantize.
            SdfShapePalette.ClearAll();
            // Slot 0 is reserved (white); fill the remaining 15 slots
            for (int i = 0; i < SdfShapePalette.MaxSlots - 1; i++)
            {
                SdfShapePalette.Resolve(new Color(i / 16f, 0.5f, 0.5f));
            }
            // The 16th unique color (17th overall counting slot 0) must throw
            Assert.Throws<UISystemPaletteOverflowException>(() =>
            {
                SdfShapePalette.Resolve(new Color(0.99f, 0.01f, 0.02f, 1f));
            });
        }
    }
}
#endif
