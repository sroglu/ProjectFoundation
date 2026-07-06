using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.UIElements;
using PFound.Render.RenderContext;

namespace PFound.Render.Tests
{
    /// <summary>
    /// T056 cross-backend visual orthogonality test (SC-003 — &lt; 2% of pixels differ by
    /// &gt; 4/255 in any channel). Builds three handles in the same scene with the SAME
    /// descriptor + SAME content prefab parented under each handle's ContentRoot, samples
    /// the three RTs, and pairwise compares.
    ///
    /// NOTE: each handle's content is a separate primitive instance; the world-space
    /// positions differ (each handle's owner GO parks at the same offset, but the cameras
    /// look at the local ContentRoot, so framing is identical). This isolates "what gets
    /// rendered" from "where the surface goes."
    /// </summary>
    public sealed class RenderContextBackendOrthogonalityTests
    {
        [UnityTest]
        public IEnumerator ThreeBackends_RenderEquivalentContent()
        {
            // Build the three surfaces
            var canvasGo = new GameObject("__canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var rawGo = new GameObject("__raw", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            rawGo.transform.SetParent(canvasGo.transform, false);
            var raw = rawGo.GetComponent<RawImage>();
            raw.rectTransform.sizeDelta = new Vector2(128, 128);

            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            var docGo = new GameObject("__doc");
            var doc = docGo.AddComponent<UIDocument>();
            doc.panelSettings = panelSettings;
            var ve = new VisualElement();
            ve.style.width = 128;
            ve.style.height = 128;
            doc.rootVisualElement.Add(ve);

            var quadGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quadGo.transform.position = new Vector3(0, 0, 5);
            var mr = quadGo.GetComponent<MeshRenderer>();

            yield return null;
            yield return null;
            yield return null;

            var svc = new RenderContextService();
            try
            {
                var desc = RenderContextDescriptor.Default;
                desc.Width = 128;
                desc.Height = 128;
                desc.Msaa = 1;
                desc.CullingMask = 1 << LayerMask.NameToLayer("RenderContext");
                desc.BackgroundColor = new Color(0.05f, 0.05f, 0.05f, 1f);

                var hA = svc.Acquire(desc, new RawImageAnchor(raw));
                var hB = svc.Acquire(desc, new VisualElementAnchor(ve));
                var hC = svc.Acquire(desc, new MeshRendererAnchor(mr));

                // Same content prefab under each ContentRoot
                AddContent(hA.ContentRoot);
                AddContent(hB.ContentRoot);
                AddContent(hC.ContentRoot);

                yield return null;
                yield return null;
                yield return null;
                hA.Camera.Render();
                hB.Camera.Render();
                hC.Camera.Render();

                var pxA = SampleRT(hA.Texture);
                var pxB = SampleRT(hB.Texture);
                var pxC = SampleRT(hC.Texture);

                AssertOrthogonal(pxA, pxB, "A(uGUI) vs B(UIToolkit)");
                AssertOrthogonal(pxA, pxC, "A(uGUI) vs C(WorldSpace)");
                AssertOrthogonal(pxB, pxC, "B(UIToolkit) vs C(WorldSpace)");

                hA.Dispose();
                hB.Dispose();
                hC.Dispose();
            }
            finally
            {
                svc.Dispose();
                UnityEngine.Object.DestroyImmediate(canvasGo);
                UnityEngine.Object.DestroyImmediate(docGo);
                UnityEngine.Object.DestroyImmediate(quadGo);
                UnityEngine.Object.DestroyImmediate(panelSettings);
            }
        }

        private static void AddContent(Transform parent)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = Vector3.zero;
            cube.layer = LayerMask.NameToLayer("RenderContext");
            var lightGo = new GameObject("__light");
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.localPosition = new Vector3(1, 2, -1);
            lightGo.transform.localRotation = Quaternion.Euler(45, -30, 0);
            lightGo.layer = LayerMask.NameToLayer("RenderContext");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
        }

        private static Color32[] SampleRT(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tx = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tx.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tx.Apply();
            RenderTexture.active = prev;
            var px = tx.GetPixels32();
            UnityEngine.Object.DestroyImmediate(tx);
            return px;
        }

        private static void AssertOrthogonal(Color32[] a, Color32[] b, string label)
        {
            Assert.AreEqual(a.Length, b.Length, $"Pixel count mismatch for {label}");
            int differing = 0;
            for (int i = 0; i < a.Length; i++)
            {
                int dr = System.Math.Abs(a[i].r - b[i].r);
                int dg = System.Math.Abs(a[i].g - b[i].g);
                int db = System.Math.Abs(a[i].b - b[i].b);
                if (dr > 4 || dg > 4 || db > 4) differing++;
            }
            float frac = differing / (float)a.Length;
            UnityEngine.Debug.Log($"[Orthogonality {label}] differing pixels: {differing}/{a.Length} ({frac:P2})");
            Assert.Less(frac, 0.02f, $"{label} differs by {frac:P2} (>2% threshold from SC-003)");
        }
    }
}
