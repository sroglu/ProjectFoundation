using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using PFound.Render.Effects.Outline;

namespace PFound.Render.PostProcess
{
    /// <summary>
    /// Built-in adapter for <see cref="OutlineRequest"/>. Drives Phase 4's
    /// <c>OutlineVolumeComponent.{Strength, EdgeColor, Thickness}</c> in one coherent push.
    /// Default policy: <c>HighestPriorityWins</c>.
    /// </summary>
    public sealed class OutlineAdapter : RenderPostProcessAdapterBase<OutlineRequest>
    {
        private OutlineVolumeComponent _resolved;
        private float _baselineStrength = float.NaN;
        private Color _baselineEdgeColor = default;
        private int _baselineThickness = -1;
        private bool _warnedMissing;
        private bool _warnedWeightedSumUnsupported;

        /// <summary>Constructs an Outline adapter with the supplied blend policy.</summary>
        public OutlineAdapter(PostProcessBlendPolicy policy = PostProcessBlendPolicy.HighestPriorityWins, int maxSlots = 256, bool warnOnMissingVolumeComponent = true)
            : base(policy, maxSlots, warnOnMissingVolumeComponent) { }

        /// <inheritdoc />
        protected override float GetFadeIn(in OutlineRequest request) => request.FadeIn;

        /// <inheritdoc />
        protected override float GetFadeOut(in OutlineRequest request) => request.FadeOut;

        /// <inheritdoc />
        public override void ResolveAndApply(IReadOnlyList<ActiveRequest<OutlineRequest>> stack, float deltaTime)
        {
            var vol = ResolveVolume();
            if (vol == null) return;

            if (float.IsNaN(_baselineStrength))
            {
                _baselineStrength = vol.Strength.value;
                _baselineEdgeColor = vol.EdgeColor.value;
                _baselineThickness = vol.Thickness.value;
            }

            if (stack.Count == 0)
            {
                vol.Strength.value = _baselineStrength;
                vol.EdgeColor.value = _baselineEdgeColor;
                vol.Thickness.value = _baselineThickness;
                return;
            }

            float resolvedStrength;
            Color resolvedColor;
            int resolvedThickness;

            switch (Policy)
            {
                case PostProcessBlendPolicy.WeightedSum:
                {
                    float sumStrength = 0f;
                    float r = 0f, g = 0f, b = 0f, a = 0f, totalW = 0f;
                    int maxPriority = int.MinValue;
                    int highestPrioThickness = _baselineThickness;
                    for (int i = 0; i < stack.Count; i++)
                    {
                        var e = stack[i];
                        sumStrength += e.Request.Strength * e.FadeWeight;
                        r += e.Request.EdgeColor.r * e.FadeWeight;
                        g += e.Request.EdgeColor.g * e.FadeWeight;
                        b += e.Request.EdgeColor.b * e.FadeWeight;
                        a += e.Request.EdgeColor.a * e.FadeWeight;
                        totalW += e.FadeWeight;
                        if (e.Priority > maxPriority) { maxPriority = e.Priority; highestPrioThickness = e.Request.Thickness; }
                    }
                    resolvedStrength = Mathf.Clamp01(sumStrength);
                    resolvedColor = totalW > 0f ? new Color(r / totalW, g / totalW, b / totalW, a / totalW) : _baselineEdgeColor;
                    resolvedThickness = highestPrioThickness;
                    break;
                }
                case PostProcessBlendPolicy.LatestWins:
                {
                    var last = stack[stack.Count - 1];
                    resolvedStrength = Mathf.Lerp(_baselineStrength, last.Request.Strength, last.FadeWeight);
                    resolvedColor = Color.Lerp(_baselineEdgeColor, last.Request.EdgeColor, last.FadeWeight);
                    resolvedThickness = last.Request.Thickness;
                    break;
                }
                case PostProcessBlendPolicy.HighestPriorityWins:
                default:
                {
                    int best = 0;
                    for (int i = 1; i < stack.Count; i++)
                        if (stack[i].Priority >= stack[best].Priority) best = i;
                    var picked = stack[best];
                    resolvedStrength = Mathf.Lerp(_baselineStrength, picked.Request.Strength, picked.FadeWeight);
                    resolvedColor = Color.Lerp(_baselineEdgeColor, picked.Request.EdgeColor, picked.FadeWeight);
                    resolvedThickness = picked.Request.Thickness;
                    break;
                }
            }

            vol.Strength.value = resolvedStrength;
            vol.EdgeColor.value = resolvedColor;
            vol.Thickness.value = resolvedThickness;
        }

        private OutlineVolumeComponent ResolveVolume()
        {
            if (_resolved != null) return _resolved;
            var stack = VolumeManager.instance?.stack;
            _resolved = stack != null ? stack.GetComponent<OutlineVolumeComponent>() : null;
            if (_resolved == null && WarnOnMissingVolumeComponent && !_warnedMissing)
            {
                _warnedMissing = true;
                Debug.LogWarning("[Render.PostProcess] OutlineAdapter: OutlineVolumeComponent missing from the active Volume Profile; adapter will no-op until corrected.");
            }
            return _resolved;
        }

        /// <summary>Test-only injection (mirrors <see cref="BlurAdapter.SetVolumeForTesting"/>).</summary>
        internal void SetVolumeForTesting(OutlineVolumeComponent vol)
        {
            _resolved = vol;
            _baselineStrength = float.NaN;
            _baselineThickness = -1;
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            _resolved = null;
        }
    }
}
