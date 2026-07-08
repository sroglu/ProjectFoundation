using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using PFound.Render.Core.Pipeline;
using PFound.Render.Core.RenderTextures;

namespace PFound.Render.Effects.Blur
{
    /// <summary>
    /// URP RendererFeature for screen-space Gaussian blur. Pairs with
    /// <see cref="BlurStrengthVolumeComponent"/> on a Volume Profile for runtime control.
    /// </summary>
    [Serializable]
    public sealed class BlurRenderFeature : RenderFeatureBase
    {
        [SerializeField]
        [Tooltip("Where in the URP pipeline the blur runs. Default AfterRenderingPostProcessing applies after bloom/tonemapping.")]
        private RenderPassEvent _injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;

        [SerializeField]
        [Tooltip("Downsample factor used when the volume's OverrideQualitySettings is false.")]
        private BlurDownsample _defaultDownsample = BlurDownsample.x2;

        [SerializeField, Range(1, 4)]
        [Tooltip("Iteration count used when the volume's OverrideQualitySettings is false.")]
        private int _defaultIterations = 2;

        [SerializeField]
        [Tooltip("Emit a one-shot Debug.LogWarning if the camera target is too small for blur. Default: editor + dev builds only.")]
        private bool _warnOnInvalidTarget = true;

        private RenderTexturePool _pool;

        protected override void OnCreate()
        {
            _pool = new RenderTexturePool();
            var mat = LoadMaterial("Hidden/Render/Blur");
            var effectiveWarn = _warnOnInvalidTarget && Debug.isDebugBuild;
            EnqueuePass(new BlurPass(mat, _pool, _defaultDownsample, _defaultIterations, effectiveWarn, _injectionPoint));
        }

        protected override void OnDispose()
        {
            _pool?.Dispose();
            _pool = null;
        }
    }
}