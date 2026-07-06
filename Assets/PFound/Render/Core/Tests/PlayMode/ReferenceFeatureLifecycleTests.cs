using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using PFound.Render.Core.ReferenceFeature;

namespace PFound.Render.Core.Tests
{
    public sealed class ReferenceFeatureLifecycleTests : PlayModeTestFixture
    {
        [UnityTest]
        public IEnumerator ReferenceFeature_Created_DisposesCleanly()
        {
            var feature = ScriptableObject.CreateInstance<ReferenceRenderFeature>();
            feature.Create();
            yield return null;
            // Dispose path runs without throwing or logging errors.
            Object.DestroyImmediate(feature);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ReferenceFeature_RunsOneFrame_WithoutErrors()
        {
            // Build a URP renderer with the reference feature installed.
            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            rendererData.name = "Render.Tests.RendererData";

            var feature = ScriptableObject.CreateInstance<ReferenceRenderFeature>();
            feature.name = nameof(ReferenceRenderFeature);
            rendererData.rendererFeatures.Add(feature);

            var pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
            pipelineAsset.name = "Render.Tests.PipelineAsset";

            try
            {
                UseRenderPipelineAsset(pipelineAsset);
                // Let one frame render.
                yield return null;
                yield return null;
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.DestroyImmediate(pipelineAsset);
                Object.DestroyImmediate(feature);
                Object.DestroyImmediate(rendererData);
            }
        }
    }
}
