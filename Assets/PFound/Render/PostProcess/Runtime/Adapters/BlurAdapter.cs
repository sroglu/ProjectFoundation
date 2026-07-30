using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using PFound.Render.Effects.Blur;

namespace PFound.Render.PostProcess
{
    /// <summary>
    /// Built-in adapter for <see cref="BlurRequest"/>. Drives Phase 3's
    /// <c>BlurStrengthVolumeComponent.Strength.value</c>. Default policy: <c>HighestPriorityWins</c>.
    /// </summary>
    public sealed class BlurAdapter : RenderPostProcessAdapterBase<BlurRequest>
    {
        private BlurStrengthVolumeComponent _resolved;
        private float _baselineStrength = float.NaN;
        private bool _warnedMissing;

        /// <summary>Constructs a Blur adapter with the supplied blend policy.</summary>
        public BlurAdapter(PostProcessBlendPolicy policy = PostProcessBlendPolicy.HighestPriorityWins, int maxSlots = 256, bool warnOnMissingVolumeComponent = true)
            : base(policy, maxSlots, warnOnMissingVolumeComponent) { }

        /// <inheritdoc />
        protected override float GetFadeIn(in BlurRequest request) => request.FadeIn;

        /// <inheritdoc />
        protected override float GetFadeOut(in BlurRequest request) => request.FadeOut;

        /// <inheritdoc />
        public override void ResolveAndApply(IReadOnlyList<ActiveRequest<BlurRequest>> stack, float deltaTime)
        {
            var vol = ResolveVolume();
            if (vol == null) return;

            if (float.IsNaN(_baselineStrength)) _baselineStrength = vol.Strength.value;

            if (stack.Count == 0)
            {
                vol.Strength.value = _baselineStrength;
                return;
            }

            float result;
            switch (Policy)
            {
                case PostProcessBlendPolicy.WeightedSum:
                {
                    float sum = 0f;
                    for (int i = 0; i < stack.Count; i++) sum += stack[i].Request.Strength * stack[i].FadeWeight;
                    result = Mathf.Clamp01(sum);
                    break;
                }
                case PostProcessBlendPolicy.LatestWins:
                {
                    var last = stack[stack.Count - 1];
                    result = Mathf.Lerp(_baselineStrength, last.Request.Strength, last.FadeWeight);
                    break;
                }
                case PostProcessBlendPolicy.HighestPriorityWins:
                default:
                {
                    int best = 0;
                    for (int i = 1; i < stack.Count; i++)
                        if (stack[i].Priority >= stack[best].Priority) best = i;
                    result = Mathf.Lerp(_baselineStrength, stack[best].Request.Strength, stack[best].FadeWeight);
                    break;
                }
            }

            vol.Strength.value = result;
        }

        private BlurStrengthVolumeComponent ResolveVolume()
        {
            if (_resolved != null) return _resolved;
            var stack = VolumeManager.instance?.stack;
            _resolved = stack != null ? stack.GetComponent<BlurStrengthVolumeComponent>() : null;
            if (_resolved == null && WarnOnMissingVolumeComponent && !_warnedMissing)
            {
                _warnedMissing = true;
                Debug.LogWarning("[Render.PostProcess] BlurAdapter: BlurStrengthVolumeComponent missing from the active Volume Profile; adapter will no-op until corrected.");
            }
            return _resolved;
        }

        /// <summary>
        /// Test-only injection: bypass <see cref="VolumeManager"/> lookup. EditMode tests use this
        /// because <c>VolumeManager.instance.stack</c> is not populated outside Play mode.
        /// </summary>
        internal void SetVolumeForTesting(BlurStrengthVolumeComponent vol)
        {
            _resolved = vol;
            _baselineStrength = float.NaN; // re-capture on next ResolveAndApply
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            _resolved = null;
        }
    }
}
