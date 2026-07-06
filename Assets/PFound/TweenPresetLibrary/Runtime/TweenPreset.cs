using UnityEngine;

namespace PFound.TweenPresetLibrary
{
    /// <summary>
    /// An authored, shippable tween: up to four channels (Fade / Scale / Move / Rotate) that play together. Content,
    /// not baked code — loaded via the content-delivery address path or an injected provider (never Resources). On
    /// deserialize, channels with no playable time are reset to a clean zero default so stray authored values on an
    /// empty channel don't linger.
    /// </summary>
    [CreateAssetMenu(menuName = "PFound/Tween Preset Library/Tween Preset", fileName = "TweenPreset")]
    public sealed class TweenPreset : ScriptableObject, ISerializationCallbackReceiver
    {
        public FadeChannel Fade = new FadeChannel();
        public ScaleChannel Scale = new ScaleChannel();
        public MoveChannel Move = new MoveChannel();
        public RotateChannel Rotate = new RotateChannel();

        /// <summary>True when at least one channel has playable time.</summary>
        public bool HasAny => Fade.IsPlayable || Scale.IsPlayable || Move.IsPlayable || Rotate.IsPlayable;

        /// <summary>The longest channel's <see cref="TweenChannel{T}.TotalDuration"/> — how long the whole preset runs.</summary>
        public float MaxDuration
        {
            get
            {
                float m = Fade.TotalDuration;
                if (Scale.TotalDuration > m) m = Scale.TotalDuration;
                if (Move.TotalDuration > m) m = Move.TotalDuration;
                if (Rotate.TotalDuration > m) m = Rotate.TotalDuration;
                return m;
            }
        }

        public void OnBeforeSerialize() { }

        /// <summary>Reset any non-playable channel to a clean default (drops authored values on a zero-time channel).</summary>
        public void OnAfterDeserialize()
        {
            if (!Fade.IsPlayable) Fade = new FadeChannel();
            if (!Scale.IsPlayable) Scale = new ScaleChannel();
            if (!Move.IsPlayable) Move = new MoveChannel();
            if (!Rotate.IsPlayable) Rotate = new RotateChannel();
        }
    }
}
