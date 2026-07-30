using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using PFound.Render.Core.Pipeline;
using PFound.Render.Core.RenderTextures;

namespace PFound.Render.Effects.Overdraw
{
    /// <summary>
    /// Developer-only URP RendererFeature that renders the scene into an additive HDR
    /// accumulator and composites a per-pixel overdraw heatmap. Enabled per-renderer-asset via
    /// the inspector toggle; <b>stripped from release player builds</b> (<see cref="OnCreate"/>
    /// body wrapped in <c>#if UNITY_EDITOR || DEVELOPMENT_BUILD</c>).
    /// </summary>
    [Serializable]
    public sealed class OverdrawRenderFeature : RenderFeatureBase
    {
        [SerializeField]
        [Tooltip("Master toggle. When false, no pass is enqueued (zero per-frame cost).")]
        private bool _enable = false;

        [SerializeField]
        [Tooltip("Where in the URP pipeline the overdraw composite runs. Default preempts post-process so the heatmap is the final visible output.")]
        private RenderPassEvent _injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

        [SerializeField, Range(0.001f, 1f)]
        [Tooltip("Constant value each fragment writes into the additive accumulator. Default 0.0625 = 1/16, saturates at 16 fragment contributions per pixel.")]
        private float _contributionScalar = 0.0625f;

        [SerializeField]
        [Tooltip("Threshold-color ramp. Up to 8 (threshold, color) pairs; default 4 tiers green/yellow/orange/red at 1/3/5/8.")]
        private OverdrawThresholdEntry[] _thresholds = DefaultThresholds();

        private RenderTexturePool _pool;

        /// <summary>
        /// Returns the default 4-tier ramp: green at 1, yellow at 3, orange at 5, red at 8.
        /// Used as the initial value for the <c>_thresholds</c> serialized field; static so
        /// EditMode tests can validate the contract without instantiating a renderer feature.
        /// </summary>
        public static OverdrawThresholdEntry[] DefaultThresholds()
        {
            return new[]
            {
                new OverdrawThresholdEntry(1f, Color.green),
                new OverdrawThresholdEntry(3f, Color.yellow),
                new OverdrawThresholdEntry(5f, new Color(1f, 0.5f, 0f, 1f)), // orange
                new OverdrawThresholdEntry(8f, Color.red),
            };
        }

        /// <inheritdoc />
        protected override void OnCreate()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_enable) return;
            if (_thresholds == null || _thresholds.Length == 0) return;

            _pool = new RenderTexturePool();
            var mat = LoadMaterial("Hidden/Render/Overdraw");
            EnqueuePass(new OverdrawPass(mat, _pool, _contributionScalar, _thresholds, _injectionPoint));
#endif
        }

        /// <inheritdoc />
        protected override void OnDispose()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _pool?.Dispose();
            _pool = null;
#endif
        }
    }
}
