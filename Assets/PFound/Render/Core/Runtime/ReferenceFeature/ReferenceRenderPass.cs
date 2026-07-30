using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using PFound.Render.Core.Pipeline;

namespace PFound.Render.Core.ReferenceFeature
{
    /// <summary>
    /// Canonical no-op pass demonstrating the <see cref="RenderPassBase{TPassData}"/>
    /// pattern end-to-end. Phase 1 ships this as a template, not as a useful effect.
    /// </summary>
    /// <remarks>
    /// Subclasses authoring real passes follow the same pattern:
    /// 1. Define a pass-data DTO class for per-frame inputs.
    /// 2. Fill it in <see cref="Populate"/> from constructor state + frame data.
    /// 3. Record commands in <see cref="Execute"/>.
    /// </remarks>
    public sealed class ReferenceRenderPass : RenderPassBase<ReferenceRenderPass.PassData>
    {
        /// <summary>Per-frame pass data. Phase 1 reference: empty placeholder.</summary>
        public sealed class PassData
        {
            /// <summary>Frame number captured by Populate (kept so Execute has at least one field to read).</summary>
            public int FrameNumber;
        }

        /// <summary>Constructs the reference pass with a fixed tag and injection point.</summary>
        public ReferenceRenderPass()
            : base(passTag: "Render.ReferenceRenderPass",
                   injectionPoint: RenderPassEvent.AfterRenderingTransparents)
        {
        }

        /// <inheritdoc />
        protected override void Populate(
            IRasterRenderGraphBuilder builder,
            ref PassData data,
            ContextContainer frameData)
        {
            data.FrameNumber = UnityEngine.Time.frameCount;
            // Reference impl: no resource read/write declarations — pure no-op.
            // Real passes call builder.UseTexture(...) / builder.SetRenderAttachment(...) here.
            builder.AllowPassCulling(true);
        }

        /// <inheritdoc />
        protected override void Execute(RasterCommandBuffer cmd, in PassData data)
        {
            // Reference impl: no draw commands. Real passes call cmd.DrawProcedural / Blit / etc.
            _ = data.FrameNumber; // silence unused warning, keep the data flow visible.
        }
    }
}
