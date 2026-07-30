using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using PFound.Render.Core.Pipeline;
using PFound.Render.Core.RenderTextures;

namespace PFound.Render.Effects.Outline
{
    /// <summary>
    /// URP RendererFeature for screen-space depth-edge outline. Pairs with
    /// <see cref="OutlineVolumeComponent"/> on a Volume Profile for runtime control.
    /// Output is always composited into the camera color attachment (no global publish path).
    /// </summary>
    [Serializable]
    public sealed class OutlineRenderFeature : RenderFeatureBase
    {
        [SerializeField]
        [Tooltip("Where in the URP pipeline the outline runs. Default AfterRenderingPostProcessing applies after bloom/tonemapping.")]
        private RenderPassEvent _injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;

        [SerializeField, Range(1, 16)]
        [Tooltip("Fallback sample radius (in texels) when the active volume has no usable Thickness.")]
        private int _defaultThickness = 2;

        [SerializeField]
        [Tooltip("Emit a one-shot Debug.LogWarning when the active volume holds an invalid Thickness. Default: editor + dev builds only.")]
        private bool _warnOnInvalidConfig = true;

        private RenderTexturePool _pool;

        /// <inheritdoc />
        protected override void OnCreate()
        {
            _pool = new RenderTexturePool();
            var mat = LoadMaterial("Hidden/Render/Outline");
            var effectiveWarn = _warnOnInvalidConfig && Debug.isDebugBuild;
            EnqueuePass(new OutlinePass(mat, _pool, _defaultThickness, effectiveWarn, _injectionPoint));
        }

        /// <inheritdoc />
        protected override void OnDispose()
        {
            _pool?.Dispose();
            _pool = null;
        }
    }
}
