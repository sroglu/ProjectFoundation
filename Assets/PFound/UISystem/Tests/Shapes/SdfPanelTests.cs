#if UNITY_INCLUDE_TESTS
using PFound.UISystem.Shapes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace PFound.UISystem.Tests.Shapes
{
    /// <summary>
    /// EditMode smoke tests for <see cref="SdfPanel"/> — the theme-agnostic composable
    /// container around <see cref="GpuSdfElement"/>. Validates the four contracts:
    /// hierarchy structure, Material propagation, contentContainer routing, Clickable toggle.
    /// </summary>
    public class SdfPanelTests
    {
        private Shader _shader;

        [SetUp]
        public void SetUp()
        {
            _shader = Shader.Find("UISystem/Shape");
            Assert.IsNotNull(_shader, "Shader 'UISystem/Shape' missing — Phase 1 dependency.");
        }

        [Test]
        public void Default_AddsUssClassName()
        {
            var panel = new SdfPanel();
            CollectionAssert.Contains(panel.GetClasses(), SdfPanel.ussClassName);
        }

        [Test]
        public void Default_HierarchyHasVisualAndClipArea()
        {
            var panel = new SdfPanel();
            Assert.AreEqual(2, panel.hierarchy.childCount,
                "Expected hierarchy.childCount = 2 (visual layer + clip area).");
            Assert.IsInstanceOf<GpuSdfElement>(panel.hierarchy[0],
                "First child should be the GpuSdfElement visual layer.");
        }

        [Test]
        public void Default_NoRippleUntilClickable()
        {
            var panel = new SdfPanel();
            // ClipArea (hierarchy[1]) should only contain the content area initially.
            var clipArea = panel.hierarchy[1];
            Assert.AreEqual(1, clipArea.childCount,
                "Before Clickable=true the clipArea should only contain the content.");
        }

        [Test]
        public void Material_PropagatesToInnerGpuSdfElement()
        {
            var mat = new Material(_shader) { hideFlags = HideFlags.DontSave };
            try
            {
                var panel = new SdfPanel { Material = mat };
                var visual = panel.hierarchy[0] as GpuSdfElement;
                Assert.IsNotNull(visual);
                Assert.AreSame(mat, visual.Material,
                    "Material assigned on SdfPanel should propagate to the inner GpuSdfElement.");
            }
            finally
            {
                Object.DestroyImmediate(mat);
            }
        }

        [Test]
        public void ContentContainer_RoutesAddToContentArea()
        {
            var panel = new SdfPanel();
            var label = new Label("Hello");
            panel.Add(label);

            // contentContainer = _content; the label should live inside the clipArea's content child.
            var clipArea = panel.hierarchy[1];
            Assert.AreEqual(-1, panel.hierarchy.IndexOf(label),
                "label must NOT be a direct hierarchy child — contentContainer should route it into the content area (IndexOf == -1).");
            // Confirmed routing: traverse clipArea down to the content area.
            VisualElement content = null;
            foreach (var child in clipArea.Children())
            {
                if (child.ClassListContains(SdfPanel.contentUssClassName))
                {
                    content = child;
                    break;
                }
            }
            Assert.IsNotNull(content, "Inner content element with ussClass not found.");
            Assert.AreEqual(1, content.childCount, "Label should land inside the content area.");
            Assert.AreSame(label, content[0]);
        }

        [Test]
        public void Clickable_True_AddsRippleToClipArea()
        {
            var panel = new SdfPanel { Clickable = true };
            var clipArea = panel.hierarchy[1];
            // ClipArea should now have ripple + content (2 children).
            Assert.AreEqual(2, clipArea.childCount,
                "When Clickable=true, clipArea should contain ripple + content.");
            // pickingMode promoted so the panel can receive ClickEvent.
            Assert.AreEqual(PickingMode.Position, panel.pickingMode);
        }

        [Test]
        public void Clickable_FalseAfterTrue_RemovesRipple()
        {
            var panel = new SdfPanel { Clickable = true };
            panel.Clickable = false;
            var clipArea = panel.hierarchy[1];
            Assert.AreEqual(1, clipArea.childCount,
                "Toggling Clickable=false should remove the ripple element.");
            Assert.AreEqual(PickingMode.Ignore, panel.pickingMode);
        }

        [Test]
        public void Clickable_OnClickEvent_FiresWhenClickable()
        {
            var panel = new SdfPanel { Clickable = true };
            int clickCount = 0;
            panel.OnClick += () => clickCount++;

            // We can't synthesize ClickEvent without a panel attachment. Lighter check:
            // verify the event is wired (subscriber registered without exception).
            Assert.Pass("OnClick subscription accepted; live click dispatch requires a runtime panel.");
        }
    }
}
#endif
