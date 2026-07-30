using System.Collections.Generic;

namespace PFound.GuidedOnboardingFlow.Core.Tests
{
    internal static class RunnerTests
    {
        private static TutorialRunner Make(IReadOnlyList<ITutorialStep> steps, System.Func<bool> pre = null)
        {
            return new TutorialRunner(new TutorialInstance(new TutorialId(1), "T", steps), pre);
        }

        public static void Run()
        {
            TestKit.Run("Pending before Begin", () =>
            {
                var r = Make(Steps.List(new ProbeStep()));
                TestKit.AreEqual(RunnerPhase.Pending, r.Phase, "phase");
                TestKit.IsFalse(r.Outcome.HasValue, "no outcome yet");
            });

            TestKit.Run("Runs steps in order to Completed", () =>
            {
                var a = new ProbeStep();
                var b = new ProbeStep();
                var r = Make(Steps.List(a, b));
                r.Begin();
                TestKit.AreEqual(RunnerPhase.Active, r.Phase, "active");
                r.Tick(0.1f);
                TestKit.IsTrue(a.BeganCalled, "a began");
                a.FinishNext();
                r.Tick(0.1f);
                TestKit.IsTrue(a.CompletedCalled, "a completed");
                TestKit.IsTrue(b.BeganCalled, "b began");
                b.FinishNext();
                r.Tick(0.1f);
                TestKit.AreEqual(RunnerPhase.Ended, r.Phase, "ended");
                TestKit.AreEqual(TutorialOutcome.Completed, r.Outcome.Value, "completed");
            });

            TestKit.Run("Empty tutorial completes on Begin", () =>
            {
                var r = Make(Steps.List());
                r.Begin();
                TestKit.AreEqual(TutorialOutcome.Completed, r.Outcome.Value, "empty completes");
            });

            TestKit.Run("Skip yields Skipped and cancels current", () =>
            {
                var a = new ProbeStep();
                var r = Make(Steps.List(a));
                r.Begin();
                r.Tick(0.1f);
                r.Skip();
                TestKit.AreEqual(TutorialOutcome.Skipped, r.Outcome.Value, "skipped");
                TestKit.IsTrue(a.CancelCalled, "cancelled");
                TestKit.AreEqual(StepCancelReason.UserSkipped, a.LastCancel, "reason");
            });

            TestKit.Run("Shutdown yields Shutdown reason", () =>
            {
                var a = new ProbeStep();
                var r = Make(Steps.List(a));
                r.Begin();
                r.Tick(0.1f);
                r.NotifyShutdown();
                TestKit.AreEqual(TutorialOutcome.Shutdown, r.Outcome.Value, "shutdown");
                TestKit.AreEqual(StepCancelReason.Shutdown, a.LastCancel, "reason");
            });

            TestKit.Run("Lapsed precondition yields Invalidated", () =>
            {
                bool valid = true;
                var a = new ProbeStep();
                var r = Make(Steps.List(a), () => valid);
                r.Begin();
                r.Tick(0.1f);
                valid = false;
                r.Tick(0.1f);
                TestKit.AreEqual(TutorialOutcome.Invalidated, r.Outcome.Value, "invalidated");
                TestKit.AreEqual(StepCancelReason.RunAborted, a.LastCancel, "abort reason");
            });

            TestKit.Run("Precondition false at Begin ends Invalidated", () =>
            {
                var r = Make(Steps.List(new ProbeStep()), () => false);
                r.Begin();
                TestKit.AreEqual(TutorialOutcome.Invalidated, r.Outcome.Value, "invalidated at begin");
            });

            TestKit.Run("Step fault yields Faulted", () =>
            {
                var a = new ProbeStep(throwOnAdvance: true);
                var r = Make(Steps.List(a));
                r.Begin();
                r.Tick(0.1f);
                TestKit.AreEqual(TutorialOutcome.Faulted, r.Outcome.Value, "faulted");
            });

            TestKit.Run("Fault in Begin yields Faulted immediately", () =>
            {
                var a = new ProbeStep(throwOnBegin: true);
                var r = Make(Steps.List(a));
                r.Begin();
                r.Tick(0.1f);
                TestKit.AreEqual(TutorialOutcome.Faulted, r.Outcome.Value, "faulted on begin");
            });
        }
    }
}
