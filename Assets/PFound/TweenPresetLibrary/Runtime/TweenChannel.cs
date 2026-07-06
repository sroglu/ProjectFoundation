using System;
using PFound.TweenPresetLibrary.Core;
using UnityEngine;

namespace PFound.TweenPresetLibrary
{
    /// <summary>
    /// One authored tween channel: a typed value that eases from a start to <see cref="EndValue"/> over
    /// <see cref="Duration"/> after <see cref="Delay"/>. Generic over the value type so Fade/Scale/Move/Rotate share
    /// the timing + easing fields. Serialized by value inside a <see cref="TweenPreset"/>; the concrete subclasses are
    /// non-generic so Unity serializes them.
    /// </summary>
    [Serializable]
    public abstract class TweenChannel<T> where T : struct
    {
        [Tooltip("Seconds to wait before this channel starts.")]
        public float Delay;

        [Tooltip("Seconds the channel takes to ease from start to EndValue.")]
        public float Duration;

        public Ease Ease;

        [Tooltip("If set, the channel starts from ForcedInitialValue instead of the target's current value.")]
        public bool ForceInitialValue;

        public T ForcedInitialValue;
        public T EndValue;

        /// <summary>The channel's kind (Fade/Scale/Move/Rotate); the base itself is <see cref="TweenChannelKind.Empty"/>.</summary>
        public virtual TweenChannelKind Kind => TweenChannelKind.Empty;

        /// <summary>Delay + Duration — the wall-clock length of the channel.</summary>
        public float TotalDuration => Delay + Duration;

        /// <summary>A channel with no time does nothing; only playable channels contribute to a preset.</summary>
        public bool IsPlayable => TotalDuration > 0f;
    }

    /// <summary>Alpha fade (e.g. a CanvasGroup).</summary>
    [Serializable]
    public sealed class FadeChannel : TweenChannel<float>
    {
        public override TweenChannelKind Kind => TweenChannelKind.Fade;
    }

    /// <summary>Local scale.</summary>
    [Serializable]
    public sealed class ScaleChannel : TweenChannel<Vector3>
    {
        public override TweenChannelKind Kind => TweenChannelKind.Scale;
    }

    /// <summary>2D anchored / local move.</summary>
    [Serializable]
    public sealed class MoveChannel : TweenChannel<Vector2>
    {
        public override TweenChannelKind Kind => TweenChannelKind.Move;
    }

    /// <summary>Euler rotation (degrees).</summary>
    [Serializable]
    public sealed class RotateChannel : TweenChannel<Vector3>
    {
        public override TweenChannelKind Kind => TweenChannelKind.Rotate;
    }
}
