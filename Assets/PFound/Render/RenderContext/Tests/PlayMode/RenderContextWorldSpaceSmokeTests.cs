using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PFound.Render.RenderContext;

namespace PFound.Render.Tests
{
    /// <summary>
    /// US3 PlayMode smoke test (T041). Wraps a Quad's MeshRenderer with MeshRendererAnchor,
    /// asserts that the runtime material (not sharedMaterial asset) carries the RT, and that
    /// rendering produces non-background pixels. On dispose, sharedMaterial is restored.
    /// </summary>
    public sealed class RenderContextWorldSpaceSmokeTests
    {
        [UnityTest]
        public IEnumerator Acquire_ClonesMaterial_AndRendersContent()
        {
            var quadGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quadGo.transform.position = new Vector3(0, 0, 5);
            var mr = quadGo.GetComponent<MeshRenderer>();
            var originalShared = mr.sharedMaterial;

            var svc = new RenderContextService();
            try
            {
                var desc = RenderContextDescriptor.Default;
                desc.Width = 256;
                desc.Height = 256;
                desc.Msaa = 1;
                desc.CullingMask = 1 << LayerMask.NameToLayer("RenderContext");
                desc.BackgroundColor = new Color(0.05f, 0.05f, 0.05f, 1f);

                var anchor = new MeshRendererAnchor(mr);
                var handle = svc.Acquire(desc, anchor);

                Assert.IsTrue(handle.IsAlive);
                Assert.AreNotSame(originalShared, mr.sharedMaterial, "Bind must clone the shared material");
                bool boundOnSomeTex =
                    (mr.sharedMaterial.HasProperty("_MainTex") && mr.sharedMaterial.GetTexture("_MainTex") == handle.Texture) ||
                    (mr.sharedMaterial.HasProperty("_BaseMap") && mr.sharedMaterial.GetTexture("_BaseMap") == handle.Texture);
                Assert.IsTrue(boundOnSomeTex, "Clone must carry handle.Texture on _MainTex or _BaseMap");

                // Render content
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
                Assert.Greater(nonBg, 500, $"Expected >500 non-background pixels in RT, got {nonBg}");

                handle.Dispose();
                Assert.AreSame(originalShared, mr.sharedMaterial, "Dispose must restore the original sharedMaterial");
            }
            finally
            {
                svc.Dispose();
                UnityEngine.Object.DestroyImmediate(quadGo);
            }
        }
    }
}
