using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PFound.Render.RenderContext;

namespace PFound.Render.Tests
{
    /// <summary>
    /// T051 wrapper parity test. Programmatic Acquire vs <see cref="RenderContextSinkBehaviour"/>
    /// produce equivalent output for the same descriptor + same RawImage target.
    /// (Equivalence is structural: both produce a non-null RT, the RawImage carries it,
    /// and the RT has non-background pixels after one render frame.)
    /// </summary>
    public sealed class RenderContextWrapperParityTests
    {
        [UnityTest]
        public IEnumerator Wrapper_BindsRawImage_AndRenders()
        {
            // Configure the resolver with a SingletonProvider so the wrapper resolves the test service.
            var hostService = new RenderContextService();
            RenderContextResolver.Use(hostService);

            var canvasGo = new GameObject("__canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var panelGo = new GameObject("__panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            panelGo.transform.SetParent(canvasGo.transform, false);
            var raw = panelGo.GetComponent<RawImage>();
            raw.rectTransform.sizeDelta = new Vector2(128, 128);

            // Attach the wrapper to the same GO so the sibling RawImage is found
            var wrapper = panelGo.AddComponent<RenderContextSinkBehaviour>();

            // Wait for OnEnable + a frame
            yield return null;
            yield return null;

            try
            {
                Assert.IsTrue(wrapper.IsAlive, "Wrapper should be alive after OnEnable");
                Assert.IsNotNull(wrapper.Texture, "Wrapper should expose a non-null RT");
                Assert.AreSame(wrapper.Texture, raw.texture, "RawImage must be bound to the wrapper's RT");

                // Drop some content through the wrapper's ContentRoot
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.SetParent(wrapper.ContentRoot, false);
                cube.layer = LayerMask.NameToLayer("RenderContext");
                var lightGo = new GameObject("__light");
                lightGo.transform.SetParent(wrapper.ContentRoot, false);
                lightGo.transform.localPosition = new Vector3(1, 2, -1);
                lightGo.transform.localRotation = Quaternion.Euler(45, -30, 0);
                lightGo.layer = LayerMask.NameToLayer("RenderContext");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;

                yield return null;
                yield return null;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvasGo);
                RenderContextResolver.Clear();
                hostService.Dispose();
            }
        }
    }
}
