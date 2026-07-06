using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace PFound.Render.Core.Pipeline
{
    /// <summary>
    /// Abstract base for URP 17 RenderGraph <see cref="ScriptableRenderPass"/>
    /// subclasses. The base owns RenderGraph builder setup; subclasses implement
    /// exactly two hooks: <see cref="Populate"/> (fills the pass-data DTO and
    /// declares resource reads/writes on the builder) and <see cref="Execute"/>
    /// (records draw commands).
    /// </summary>
    /// <typeparam name="TPassData">
    /// Reference-typed pass-data DTO. URP's RenderGraph allocates / pools instances
    /// of <typeparamref name="TPassData"/> automatically; subclasses MUST treat them
    /// as transient and MUST NOT retain references across frames.
    /// </typeparam>
    public abstract class RenderPassBase<TPassData> : ScriptableRenderPass
        where TPassData : class, new()
    {
        private readonly string _passTag;
        private BaseRenderFunc<TPassData, RasterGraphContext> _cachedRenderFunc;

        /// <summary>
        /// Constructs the pass with a tag (defaults to the runtime type name) and
        /// an injection point (defaults to <see cref="RenderPassEvent.AfterRenderingTransparents"/>).
        /// </summary>
        protected RenderPassBase(
            string passTag = null,
            RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingTransparents)
        {
            _passTag = string.IsNullOrEmpty(passTag) ? GetType().Name : passTag;
            renderPassEvent = injectionPoint;
        }

        /// <summary>
        /// Subclass fills <paramref name="data"/> from <paramref name="frameData"/> +
        /// any subclass-held state and declares resource reads/writes on
        /// <paramref name="builder"/>.
        /// </summary>
        protected abstract void Populate(
            IRasterRenderGraphBuilder builder,
            ref TPassData data,
            ContextContainer frameData);

        /// <summary>
        /// Subclass records draw commands. Called by URP's RenderGraph executor.
        /// Subclasses MUST NOT capture <c>this</c> beyond the parameters provided
        /// (the base caches a non-capturing delegate to keep the hot path
        /// allocation-free).
        /// </summary>
        protected abstract void Execute(RasterCommandBuffer cmd, in TPassData data);

        /// <inheritdoc />
        public sealed override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            using var builder = renderGraph.AddRasterRenderPass<TPassData>(_passTag, out var passData);
            Populate(builder, ref passData, frameData);

            _cachedRenderFunc ??= ExecuteAdapter;
            builder.SetRenderFunc(_cachedRenderFunc);
        }

        private void ExecuteAdapter(TPassData data, RasterGraphContext ctx)
        {
            Execute(ctx.cmd, in data);
        }
    }
}
