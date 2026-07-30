using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using PFound.Render.Core.Pipeline;

namespace PFound.Render.BatchRendering
{
    /// <summary>
    /// Per-frame DTO for the RenderGraph pass. Pooled by URP's RenderGraph; treated as transient.
    /// </summary>
    internal sealed class BatchRenderingPassData
    {
        public Camera Camera;
        public bool HasService;
    }

    /// <summary>
    /// RenderGraph pass body executed by <see cref="BatchRenderingFeature"/>. Resolves the
    /// attached service and calls
    /// <see cref="BatchRenderingService.ExecuteRenderGraphBatches"/> with the active camera.
    /// </summary>
    /// <remarks>
    /// Pass body is a clean no-op when no service is attached or the attached service has been
    /// disposed (weak ref dead). A one-shot informational diagnostic identifies the missing wiring
    /// (FR-022).
    /// </remarks>
    internal sealed class BatchRenderingRenderGraphPass : RenderPassBase<BatchRenderingPassData>
    {
        private readonly BatchRenderingFeature _feature;

        internal BatchRenderingRenderGraphPass(BatchRenderingFeature feature, RenderPassEvent injectionPoint)
            : base("BatchRendering.RenderGraphPass", injectionPoint)
        {
            _feature = feature;
        }

        protected override void Populate(IRasterRenderGraphBuilder builder, ref BatchRenderingPassData data, ContextContainer frameData)
        {
            var camData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();

            data.Camera = camData != null ? camData.camera : null;
            data.HasService = _feature != null && _feature.TryGetService(out _);

            // Bind the camera color attachment as writeable so we draw into it.
            if (resourceData.activeColorTexture.IsValid())
            {
                builder.SetRenderAttachment(resourceData.activeColorTexture, index: 0);
            }
            if (resourceData.activeDepthTexture.IsValid())
            {
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture);
            }

            // Allow the pass to run even on a "no service" frame so the diagnostic fires; otherwise
            // builder.AllowPassCulling(false) wouldn't be strictly necessary, but we keep it
            // explicit for clarity.
            builder.AllowPassCulling(false);
        }

        protected override void Execute(RasterCommandBuffer cmd, in BatchRenderingPassData data)
        {
            if (_feature == null) return;

            if (!_feature.TryGetService(out var service))
            {
                // FR-022 — one-shot diagnostic when the feature is present but no service attached.
                OneShotWarnings.WarnFeatureWithoutService(_feature.Diagnostics, _feature.FeatureId, _feature.name);
                return;
            }

            if (data.Camera == null) return;
            service.ExecuteRenderGraphBatches(cmd, data.Camera);
        }
    }
}
