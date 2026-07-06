using PFound.Render.Core.Pipeline;

namespace PFound.Render.Core.ReferenceFeature
{
    /// <summary>
    /// Canonical <see cref="RenderFeatureBase"/> subclass demonstrating the
    /// authoring pattern end-to-end. Ships a single no-op <see cref="ReferenceRenderPass"/>.
    /// Phase 1 documentation — not a useful effect.
    /// </summary>
    public sealed class ReferenceRenderFeature : RenderFeatureBase
    {
        /// <inheritdoc />
        protected override void OnCreate()
        {
            EnqueuePass(new ReferenceRenderPass());
        }
    }
}
