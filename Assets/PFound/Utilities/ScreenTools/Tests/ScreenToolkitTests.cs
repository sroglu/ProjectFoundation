using NUnit.Framework;
using PFound.Utilities.ScreenTools;
using UnityEngine;

namespace PFound.Utilities.ScreenTools.Tests
{
    public class ScreenToolkitTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void ApplySafeArea_FullScreen_MapsToUnitAnchors()
        {
            var go = new GameObject("safe", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            var screen = new Vector2(1080f, 1920f);

            ScreenToolkit.ApplySafeArea(rt, new Rect(0f, 0f, 1080f, 1920f), screen);

            Assert.AreEqual(Vector2.zero, rt.anchorMin);
            Assert.AreEqual(Vector2.one, rt.anchorMax);
            Assert.AreEqual(Vector2.zero, rt.offsetMin);
            Assert.AreEqual(Vector2.zero, rt.offsetMax);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ApplySafeArea_InsetArea_MapsToFractionalAnchors()
        {
            var go = new GameObject("safe", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            var screen = new Vector2(1000f, 2000f);

            // 100px inset on the left, 200px notch on top.
            ScreenToolkit.ApplySafeArea(rt, new Rect(100f, 0f, 900f, 1800f), screen);

            Assert.AreEqual(0.1f, rt.anchorMin.x, Tolerance);
            Assert.AreEqual(0f, rt.anchorMin.y, Tolerance);
            Assert.AreEqual(1f, rt.anchorMax.x, Tolerance);
            Assert.AreEqual(0.9f, rt.anchorMax.y, Tolerance);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void GetCenteredRect_InContainerSize_IsCentered()
        {
            Rect rect = ScreenToolkit.GetCenteredRect(new Vector2(100f, 40f), new Vector2(500f, 240f));

            Assert.AreEqual(new Vector2(200f, 100f), rect.position);
            Assert.AreEqual(new Vector2(100f, 40f), rect.size);
            Assert.AreEqual(new Vector2(250f, 120f), rect.center);
        }

        [Test]
        public void GetCenteredRect_InContainerRect_RespectsOrigin()
        {
            var container = new Rect(10f, 20f, 200f, 200f);

            Rect rect = ScreenToolkit.GetCenteredRect(new Vector2(50f, 50f), container);

            Assert.AreEqual(container.center, rect.center);
            Assert.AreEqual(new Vector2(85f, 95f), rect.position);
        }

        [Test]
        public void GetActiveDisplayIndex_IsNonNegative()
        {
            Assert.GreaterOrEqual(ScreenToolkit.GetActiveDisplayIndex(), 0);
        }
    }
}
