using NUnit.Framework;
using PFound.GuidedOnboardingFlow.Core;
using UnityEngine;

namespace PFound.GuidedOnboardingFlow.Tests
{
    /// <summary>EditMode coverage of the interface-driven step layer using fakes — no scene required.</summary>
    public sealed class StepLayerEditModeTests
    {
        [Test]
        public void AnchorRegistry_RegisterResolveUnregister()
        {
            var reg = new TutorialAnchorRegistry();
            var go = new GameObject("anchor", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();

            Assert.IsFalse(reg.TryResolve("play", out _), "unknown tag");
            reg.Register("play", rt);
            Assert.IsTrue(reg.TryResolve("play", out RectTransform got) && got == rt, "resolves");
            reg.Unregister("play");
            Assert.IsFalse(reg.TryResolve("play", out _), "gone after unregister");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void FocusStep_DefersUntilAnchorPresent_ThenLightsUpAffordances()
        {
            var services = Fakes.Services(out var blocker, out var hand, out var highlight, out _, out var anchors, out _);
            var step = new FocusStep(services, "play");

            Assert.AreEqual(StepReadiness.Deferred, step.CheckReadiness(), "defers while anchor absent");

            var go = new GameObject("play", typeof(RectTransform));
            anchors.Register("play", go.GetComponent<RectTransform>());
            Assert.AreEqual(StepReadiness.Ready, step.CheckReadiness(), "ready once present");

            step.Begin();
            Assert.IsTrue(blocker.Enabled, "blocker enabled");
            Assert.IsNotNull(highlight.Target, "highlighted");
            Assert.IsNotNull(hand.Pointed, "hand pointing");

            blocker.PendingInteraction = true;
            Assert.AreEqual(StepStatus.Finished, step.Advance(0.016f), "finishes on interaction");
            step.Complete();
            Assert.IsFalse(blocker.Enabled, "blocker disabled on teardown");
            Assert.IsTrue(highlight.Cleared, "highlight cleared");
            Assert.IsTrue(hand.Hidden, "hand hidden");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void FocusStep_AutoAdvanceCompletesWithoutInteraction()
        {
            var services = Fakes.Services(out _, out _, out _, out _, out var anchors, out var manager);
            var go = new GameObject("play", typeof(RectTransform));
            anchors.Register("play", go.GetComponent<RectTransform>());
            manager.AutoAdvance = true;

            var step = new FocusStep(services, "play");
            step.Begin();
            Assert.AreEqual(StepStatus.Finished, step.Advance(0.016f), "auto-advances");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void DialogStep_ShowsWaitsForContinue_HidesOnComplete()
        {
            var services = Fakes.Services(out _, out _, out _, out var dialog, out _, out _);
            var step = new DialogStep(services, "hello");

            step.Begin();
            Assert.IsTrue(dialog.Visible, "shown");
            Assert.AreEqual("hello", dialog.Message, "message set");
            Assert.AreEqual(StepStatus.Running, step.Advance(0.016f), "waits");

            dialog.PendingContinue = true;
            Assert.AreEqual(StepStatus.Finished, step.Advance(0.016f), "continues");
            step.Complete();
            Assert.IsFalse(dialog.Visible, "hidden");
        }

        [Test]
        public void DialogStep_CancelHidesPanel()
        {
            var services = Fakes.Services(out _, out _, out _, out var dialog, out _, out _);
            var step = new DialogStep(services, "hello");
            step.Begin();
            step.Cancel(StepCancelReason.UserSkipped);
            Assert.IsFalse(dialog.Visible, "hidden on cancel");
        }
    }
}
