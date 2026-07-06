using NUnit.Framework;
using PFound.TweenPresetLibrary.Core;
using UnityEngine;

namespace PFound.TweenPresetLibrary.Tests
{
    /// <summary>
    /// The optional applier: ticked over a fake time source (injected delta) against a fake target, each playable
    /// channel reaches its EndValue, honors per-channel delay (holding the initial), and respects forced vs
    /// captured-from-target initials.
    /// </summary>
    public sealed class TweenPresetPlayerTests
    {
        private sealed class FakeTarget : ITweenTarget
        {
            public float Alpha { get; set; }
            public Vector3 Scale { get; set; }
            public Vector2 Position { get; set; }
            public Vector3 EulerAngles { get; set; }
        }

        [Test]
        public void ReachesEndValue_AcrossAllFourChannels()
        {
            var p = ScriptableObject.CreateInstance<TweenPreset>();
            try
            {
                p.Fade.Duration = 0.5f; p.Fade.EndValue = 1f; p.Fade.Ease = Ease.Linear;
                p.Scale.Duration = 0.5f; p.Scale.EndValue = new Vector3(2, 2, 2); p.Scale.Ease = Ease.OutQuad;
                p.Move.Duration = 0.5f; p.Move.EndValue = new Vector2(10, 20); p.Move.Ease = Ease.InOutSine;
                p.Rotate.Duration = 0.5f; p.Rotate.EndValue = new Vector3(0, 0, 90); p.Rotate.Ease = Ease.OutBack;

                var t = new FakeTarget { Scale = Vector3.one };
                var player = new TweenPresetPlayer(p, t);
                for (int i = 0; i < 12; i++) player.Tick(0.05f); // 0.6s > 0.5s

                Assert.IsTrue(player.IsDone);
                Assert.AreEqual(1f, t.Alpha, 1e-4f, "Fade reached end");
                AssertVec3(new Vector3(2, 2, 2), t.Scale, "Scale reached end");
                AssertVec2(new Vector2(10, 20), t.Position, "Move reached end");
                AssertVec3(new Vector3(0, 0, 90), t.EulerAngles, "Rotate reached end (OutBack settles at 1)");
            }
            finally { Object.DestroyImmediate(p); }
        }

        [Test]
        public void HonorsDelay_HoldsInitialDuringDelay()
        {
            var p = ScriptableObject.CreateInstance<TweenPreset>();
            try
            {
                p.Fade.Delay = 0.2f; p.Fade.Duration = 0.2f; p.Fade.EndValue = 1f; p.Fade.Ease = Ease.Linear;
                p.Fade.ForceInitialValue = true; p.Fade.ForcedInitialValue = 0f;

                var t = new FakeTarget { Alpha = 0.5f };
                var player = new TweenPresetPlayer(p, t);

                player.Tick(0.1f); // elapsed 0.1 < delay 0.2 → held at the forced initial (0), not the target's 0.5
                Assert.AreEqual(0f, t.Alpha, 1e-4f, "held at initial during delay");

                player.Tick(0.2f); // elapsed 0.3 → t=(0.3-0.2)/0.2=0.5 → 0.5
                Assert.AreEqual(0.5f, t.Alpha, 1e-4f, "ramps after the delay");

                player.Tick(0.2f); // elapsed 0.5 → finished → 1
                Assert.AreEqual(1f, t.Alpha, 1e-4f, "reaches end");
            }
            finally { Object.DestroyImmediate(p); }
        }

        [Test]
        public void ForcedInitial_OverridesTargetValue()
        {
            var p = ScriptableObject.CreateInstance<TweenPreset>();
            try
            {
                p.Scale.Duration = 0.5f; p.Scale.EndValue = new Vector3(2, 2, 2); p.Scale.Ease = Ease.Linear;
                p.Scale.ForceInitialValue = true; p.Scale.ForcedInitialValue = Vector3.zero;

                var t = new FakeTarget { Scale = new Vector3(5, 5, 5) }; // current ignored because forced
                var player = new TweenPresetPlayer(p, t);
                player.Tick(0.25f); // halfway → lerp(0, 2, 0.5) = 1

                AssertVec3(new Vector3(1, 1, 1), t.Scale, "starts from forced 0, not target's 5");
            }
            finally { Object.DestroyImmediate(p); }
        }

        [Test]
        public void CapturedInitial_FromTarget_WhenNotForced()
        {
            var p = ScriptableObject.CreateInstance<TweenPreset>();
            try
            {
                p.Move.Duration = 0.5f; p.Move.EndValue = new Vector2(10, 0); p.Move.Ease = Ease.Linear;
                // not forced → the initial is captured from the target's current value

                var t = new FakeTarget { Position = new Vector2(2, 0) };
                var player = new TweenPresetPlayer(p, t);
                player.Tick(0.25f); // halfway → lerp(2, 10, 0.5) = 6

                AssertVec2(new Vector2(6, 0), t.Position, "starts from the target's current value");
            }
            finally { Object.DestroyImmediate(p); }
        }

        private static void AssertVec2(Vector2 e, Vector2 a, string m)
        {
            Assert.AreEqual(e.x, a.x, 1e-4f, m + ".x");
            Assert.AreEqual(e.y, a.y, 1e-4f, m + ".y");
        }

        private static void AssertVec3(Vector3 e, Vector3 a, string m)
        {
            Assert.AreEqual(e.x, a.x, 1e-4f, m + ".x");
            Assert.AreEqual(e.y, a.y, 1e-4f, m + ".y");
            Assert.AreEqual(e.z, a.z, 1e-4f, m + ".z");
        }
    }
}
