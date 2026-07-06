using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PFound.Render.RenderContext;

namespace PFound.Render.Tests
{
    /// <summary>
    /// US1 PlayMode smoke test (T027). Builds a Canvas + RawImage at runtime, drives the
    /// service end-to-end, asserts that the RawImage's texture is the handle's RT and that the
    /// rendered RT contains non-background pixels after a render frame.
    /// </summary>
    public sealed class RenderContextUGUISmokeTests
    {
        [UnityTest]
        public IEnumerator Acquire_BindsRtAndRendersContent()
        {
            // Arrange — minimal scene
            var canvasGo = new GameObject("__demoCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var panelGo = new GameObject("__panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            panelGo.transform.SetParent(canvasGo.transform, false);
            var raw = panelGo.GetComponent<RawImage>();
            raw.rectTransform.sizeDelta = new Vector2(256, 256);

            var svc = new RenderContextService();
            try
            {
                var desc = RenderContextDescriptor.Default;
                desc.Width = 256;
                desc.Height = 256;
                desc.Msaa = 1;
                desc.CullingMask = 1 << LayerMask.NameToLayer("RenderContext");
                desc.BackgroundColor = new Color(0.05f, 0.05f, 0.05f, 1f);

                var anchor = new RawImageAnchor(raw);
                var handle = svc.Acquire(desc, anchor);

                // Bind asserts
                Assert.IsTrue(handle.IsAlive);
                Assert.IsNotNull(handle.Texture);
                Assert.AreSame(handle.Texture, raw.texture, "RawImage.texture must equal handle.Texture after Acquire");

                // Parent a primitive cube + light under content root
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.SetParent(handle.ContentRoot, false);
                cube.transform.localPosition = Vector3.zero;
                cube.layer = LayerMask.NameToLayer("RenderContext");
                var mr = cube.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
                    { color = new Color(0.85f, 0.5f, 0.2f, 1f) };
                }
                var lightGo = new GameObject("__light");
                lightGo.transform.SetParent(handle.ContentRoot, false);
                lightGo.transform.localPosition = new Vector3(1f, 2f, -1f);
                lightGo.transform.localRotation = Quaternion.Euler(45f, -30f, 0f);
                lightGo.layer = LayerMask.NameToLayer("RenderContext");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;

                // Camera positioning is set up by SceneFactory at sensible defaults relative
                // to ContentRoot (camera local at (0, 0.4, -3.5) looking at content). Do NOT
                // touch handle.Camera.transform.position (world space) — service owner GO is
                // parked at world (10000, 10000, 10000) for camera isolation.

                // Wait for a render frame
                yield return null;
                yield return null;
                // Explicit render — PlayMode test harness may not consistently
                // tick our dedicated camera through Unity's auto-render loop.
                handle.Camera.Render();

                // Sample the RT — readback into a Texture2D
                var rt = handle.Texture;
                var prevActive = RenderTexture.active;
                RenderTexture.active = rt;
                var sample = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                sample.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                sample.Apply();
                RenderTexture.active = prevActive;

                var pixels = sample.GetPixels32();
                int nonBg = 0;
                var bg = (Color32)desc.BackgroundColor;
                for (int i = 0; i < pixels.Length; i++)
                {
                    var p = pixels[i];
                    if (System.Math.Abs(p.r - bg.r) > 8 || System.Math.Abs(p.g - bg.g) > 8 || System.Math.Abs(p.b - bg.b) > 8)
                        nonBg++;
                }
                UnityEngine.Object.DestroyImmediate(sample);

                Assert.Greater(nonBg, 500, $"Expected >500 non-background pixels in RT, got {nonBg}");

                handle.Dispose();
                Assert.IsFalse(handle.IsAlive);
            }
            finally
            {
                svc.Dispose();
                UnityEngine.Object.DestroyImmediate(canvasGo);
            }
        }
    }
}
