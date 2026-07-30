using System;
using PFound.TweenPresetLibrary.Core;
using UnityEngine;

namespace PFound.TweenPresetLibrary
{
    /// <summary>
    /// An OPTIONAL, reusable applier for an authored <see cref="TweenPreset"/> — complementary to code-driven tween
    /// libraries, for the authored-clip path. Pure C#: it is advanced by an INJECTED delta (<see cref="Tick"/>), never
    /// reading <c>Time.deltaTime</c> itself, so it is deterministic and testable. Each playable channel eases from its
    /// initial value (the target's current value, or <see cref="TweenChannel{T}.ForcedInitialValue"/> when forced) to
    /// its end value via <see cref="EaseEvaluator"/>, honoring per-channel delay and duration. Non-playable channels
    /// are left untouched.
    /// </summary>
    public sealed class TweenPresetPlayer
    {
        private readonly TweenPreset _preset;
        private readonly ITweenTarget _target;

        private bool _captured;
        private float _elapsed;
        private float _fadeFrom;
        private Vector3 _scaleFrom;
        private Vector2 _moveFrom;
        private Vector3 _rotateFrom;

        public TweenPresetPlayer(TweenPreset preset, ITweenTarget target)
        {
            _preset = preset ? preset : throw new ArgumentNullException(nameof(preset));
            _target = target ?? throw new ArgumentNullException(nameof(target));
        }

        /// <summary>Seconds elapsed since the first tick.</summary>
        public float Elapsed => _elapsed;

        /// <summary>True once elapsed has reached the preset's longest channel.</summary>
        public bool IsDone => _elapsed >= _preset.MaxDuration;

        /// <summary>Advance by <paramref name="deltaTime"/> seconds and apply every playable channel to the target.</summary>
        public void Tick(float deltaTime)
        {
            if (!_captured) CaptureInitials();
            _elapsed += deltaTime;
            Apply();
        }

        /// <summary>Re-arm: the next tick recaptures initials and time restarts from zero.</summary>
        public void Restart()
        {
            _captured = false;
            _elapsed = 0f;
        }

        private void CaptureInitials()
        {
            _captured = true;
            _fadeFrom = _preset.Fade.ForceInitialValue ? _preset.Fade.ForcedInitialValue : _target.Alpha;
            _scaleFrom = _preset.Scale.ForceInitialValue ? _preset.Scale.ForcedInitialValue : _target.Scale;
            _moveFrom = _preset.Move.ForceInitialValue ? _preset.Move.ForcedInitialValue : _target.Position;
            _rotateFrom = _preset.Rotate.ForceInitialValue ? _preset.Rotate.ForcedInitialValue : _target.EulerAngles;
        }

        private void Apply()
        {
            if (_preset.Fade.IsPlayable)
            {
                float e = Eased(_preset.Fade);
                _target.Alpha = _fadeFrom + (_preset.Fade.EndValue - _fadeFrom) * e;
            }
            if (_preset.Scale.IsPlayable)
            {
                float e = Eased(_preset.Scale);
                _target.Scale = _scaleFrom + (_preset.Scale.EndValue - _scaleFrom) * e;
            }
            if (_preset.Move.IsPlayable)
            {
                float e = Eased(_preset.Move);
                _target.Position = _moveFrom + (_preset.Move.EndValue - _moveFrom) * e;
            }
            if (_preset.Rotate.IsPlayable)
            {
                float e = Eased(_preset.Rotate);
                _target.EulerAngles = _rotateFrom + (_preset.Rotate.EndValue - _rotateFrom) * e;
            }
        }

        /// <summary>The eased 0..1 weight for a channel at the current elapsed time (0 during its delay, 1 once finished).</summary>
        private float Eased<T>(TweenChannel<T> channel) where T : struct
            => EaseEvaluator.Evaluate(channel.Ease, Progress(channel.Delay, channel.Duration));

        private float Progress(float delay, float duration)
        {
            if (_elapsed <= delay) return 0f;
            if (duration <= 0f) return 1f;
            float t = (_elapsed - delay) / duration;
            return t < 0f ? 0f : (t > 1f ? 1f : t);
        }
    }
}
