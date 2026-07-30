using NUnit.Framework;
using PFound.TweenPresetLibrary.Core;
using UnityEngine;

namespace PFound.TweenPresetLibrary.Tests
{
    /// <summary>
    /// The preset data model: per-channel timing (TotalDuration/IsPlayable/Kind), preset roll-ups (HasAny/MaxDuration),
    /// the deserialize cleanup that resets non-playable channels, and a real Unity-serialization round-trip proving the
    /// inherited generic-base fields actually persist.
    /// </summary>
    public sealed class TweenPresetTests
    {
        [Test]
        public void Channel_TotalDuration_And_IsPlayable()
        {
            var c = new FadeChannel { Delay = 0.2f, Duration = 0.5f };
            Assert.AreEqual(0.7f, c.TotalDuration, 1e-5f);
            Assert.IsTrue(c.IsPlayable);

            var zero = new FadeChannel();
            Assert.AreEqual(0f, zero.TotalDuration);
            Assert.IsFalse(zero.IsPlayable, "a zero-time channel is not playable");
        }

        [Test]
        public void Channel_Kind_IsPerConcreteType()
        {
            Assert.AreEqual(TweenChannelKind.Fade, new FadeChannel().Kind);
            Assert.AreEqual(TweenChannelKind.Scale, new ScaleChannel().Kind);
            Assert.AreEqual(TweenChannelKind.Move, new MoveChannel().Kind);
            Assert.AreEqual(TweenChannelKind.Rotate, new RotateChannel().Kind);
        }

        [Test]
        public void Preset_HasAny_And_MaxDuration()
        {
            var p = ScriptableObject.CreateInstance<TweenPreset>();
            try
            {
                Assert.IsFalse(p.HasAny, "a fresh preset has no playable channels");
                Assert.AreEqual(0f, p.MaxDuration);

                p.Fade.Duration = 0.3f;
                p.Move.Delay = 0.1f; p.Move.Duration = 0.5f; // total 0.6 → the max
                Assert.IsTrue(p.HasAny);
                Assert.AreEqual(0.6f, p.MaxDuration, 1e-5f, "max across channels");
            }
            finally { Object.DestroyImmediate(p); }
        }

        [Test]
        public void OnAfterDeserialize_ResetsNonPlayableChannels_KeepsPlayable()
        {
            var p = ScriptableObject.CreateInstance<TweenPreset>();
            try
            {
                // Non-playable (zero-time) channel carrying stray authored values.
                p.Fade.Duration = 0f; p.Fade.EndValue = 1f; p.Fade.Ease = Ease.OutBack;
                // Playable channel that must survive the cleanup.
                p.Scale.Duration = 0.4f; p.Scale.EndValue = new Vector3(2, 2, 2);

                p.OnAfterDeserialize();

                Assert.AreEqual(0f, p.Fade.EndValue, "non-playable Fade reset to default");
                Assert.AreEqual(Ease.Unset, p.Fade.Ease, "non-playable Fade ease cleared");
                Assert.AreEqual(0.4f, p.Scale.Duration, 1e-5f, "playable Scale preserved");
                Assert.AreEqual(new Vector3(2, 2, 2), p.Scale.EndValue, "playable Scale value preserved");
            }
            finally { Object.DestroyImmediate(p); }
        }

        [Test]
        public void GenericChannelFields_SurviveUnitySerialization()
        {
            var p = ScriptableObject.CreateInstance<TweenPreset>();
            try
            {
                p.Fade.Delay = 0.1f; p.Fade.Duration = 0.5f; p.Fade.Ease = Ease.InOutSine;
                p.Fade.ForceInitialValue = true; p.Fade.ForcedInitialValue = 0.2f; p.Fade.EndValue = 0.9f;
                p.Move.Duration = 0.3f; p.Move.EndValue = new Vector2(5, 6);

                string json = JsonUtility.ToJson(p); // Unity's serializer — same rules as .asset serialization
                var clone = ScriptableObject.CreateInstance<TweenPreset>();
                try
                {
                    JsonUtility.FromJsonOverwrite(json, clone);

                    Assert.AreEqual(0.5f, clone.Fade.Duration, 1e-5f, "Fade.Duration (generic-base field) survived");
                    Assert.AreEqual(Ease.InOutSine, clone.Fade.Ease, "Fade.Ease survived");
                    Assert.IsTrue(clone.Fade.ForceInitialValue, "Fade.ForceInitialValue survived");
                    Assert.AreEqual(0.9f, clone.Fade.EndValue, 1e-5f, "Fade.EndValue (T=float) survived");
                    Assert.AreEqual(new Vector2(5, 6), clone.Move.EndValue, "Move.EndValue (T=Vector2) survived");
                }
                finally { Object.DestroyImmediate(clone); }
            }
            finally { Object.DestroyImmediate(p); }
        }
    }
}
