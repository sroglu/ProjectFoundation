using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using PFound.Render.RenderContext;

namespace PFound.Render.Tests
{
    /// <summary>
    /// US2 PlayMode smoke test (T035). Builds a UIDocument + a sized VisualElement at runtime,
    /// drives the service end-to-end via VisualElementAnchor, asserts the element's background
    /// is bound to the handle's RT and that the rendered RT contains non-background pixels.
    /// </summary>
    public sealed class RenderContextUIToolkitSmokeTests
    {
        [UnityTest]
        public IEnumerator Acquire_BindsRtAsBackground_AndRendersContent()
        {
            // Arrange — UIDocument with a sized VisualElement
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;

            var docGo = new GameObject("__uidocument");
            var doc = docGo.AddComponent<UIDocument>();
            doc.panelSettings = panelSettings;

            var portrait = new VisualElement { name = "portrait" };
            portrait.style.width = 256;
            portrait.style.height = 256;
            doc.rootVisualElement.Add(portrait);

            // Wait for layout pass
            yield return null;
            yield return null;
            yield return null;

            var svc = new RenderContextService();
            try
            {
                var desc = RenderContextDescriptor.Default;
                desc.Width = 256;
                desc.Height = 256;
                desc.Msaa = 1;
                desc.CullingMask = 1 << LayerMask.NameToLayer("RenderContext");
                desc.BackgroundColor = new Color(0.05f, 0.05f, 0.05f, 1f);

                var anchor = new VisualElementAnchor(portrait);
                var handle = svc.Acquire(desc, anchor);

                Assert.IsTrue(handle.IsAlive);
                Assert.IsNotNull(handle.Texture);
                var img = portrait.Q<Image>(VisualElementSink.ChildName);
                Assert.IsNotNull(img, "Sink should attach an Image child to the VisualElement");
                Assert.AreSame(handle.Texture, img.image,
                    "Attached Image must reference handle.Texture after Acquire");

                // Parent content
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.SetParent(handle.ContentRoot, false);
                cube.transform.localPosition = Vector3.zero;
                cube.layer = LayerMask.NameToLayer("RenderContext");
                var lightGo = new GameObject("__light");
                lightGo.transform.SetParent(handle.ContentRoot, false);
                lightGo.transform.localPosition = new Vector3(1, 2, -1);
                lightGo.transform.localRotation = Quaternion.Euler(45, -30, 0);
                lightGo.layer = LayerMask.NameToLayer("RenderContext");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;

                // Wait for render frames
                yield return null;
                yield return null;
                handle.Camera.Render();

                var rt = handle.Texture;
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var sample = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                sample.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                sample.Apply();
                RenderTexture.active = prev;

                var pixels = sample.GetPixels32();
                int nonBg = 0;
                var bgC = (Color32)desc.BackgroundColor;
                for (int i = 0; i < pixels.Length; i++)
                {
                    var p = pixels[i];
                    if (System.Math.Abs(p.r - bgC.r) > 8 || System.Math.Abs(p.g - bgC.g) > 8 || System.Math.Abs(p.b - bgC.b) > 8)
                        nonBg++;
                }
                UnityEngine.Object.DestroyImmediate(sample);
                Assert.Greater(nonBg, 500, $"Expected >500 non-background pixels, got {nonBg}");

                handle.Dispose();
                Assert.IsFalse(handle.IsAlive);
            }
            finally
            {
                svc.Dispose();
                UnityEngine.Object.DestroyImmediate(docGo);
                UnityEngine.Object.DestroyImmediate(panelSettings);
            }
        }
    }
}
