using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using PFound.Render.RenderContext;
using PFound.Render.Tests.Helpers;

namespace PFound.Render.Tests
{
    /// <summary>
    /// US4 headless rendering test (T043). Pure-C# usage of the service — no scene loaded.
    /// Acquires a handle via <see cref="TestRenderContextAnchor"/>, instantiates a primitive
    /// under <c>ContentRoot</c>, manually renders the handle's camera, reads back pixels and
    /// asserts non-background content. SC-004 target is wall-clock &lt; 50ms.
    /// </summary>
    public sealed class RenderContextHeadlessRenderTests
    {
        [Test]
        public void Acquire_Render_ReadPixels_HasNonBackgroundContent()
        {
            var svc = new RenderContextService();
            try
            {
                var sw = Stopwatch.StartNew();

                var desc = RenderContextDescriptor.Default;
                desc.Width = 128;
                desc.Height = 128;
                desc.Msaa = 1;
                desc.CullingMask = 1 << LayerMask.NameToLayer("RenderContext");
                desc.BackgroundColor = new Color(0.05f, 0.05f, 0.05f, 1f);

                var anchor = new TestRenderContextAnchor(128, 128);
                var handle = svc.Acquire(desc, anchor);

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
                sw.Stop();

                Assert.Greater(nonBg, 200, $"Expected >200 non-bg pixels, got {nonBg}");
                // SC-004 is a soft target; log but do not hard-fail
                UnityEngine.Debug.Log($"[RenderContextHeadlessRenderTests] elapsed {sw.ElapsedMilliseconds}ms (target <50ms)");

                handle.Dispose();
                Assert.IsFalse(handle.IsAlive);
            }
            finally
            {
                svc.Dispose();
            }
        }
    }
}
