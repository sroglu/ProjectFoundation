using System.Collections.Generic;

namespace PFound.GuidedOnboardingFlow.Core.Tests
{
    internal static class AchievabilityTests
    {
        private static TutorialRunner Make(IReadOnlyList<ITutorialStep> steps)
        {
            return new TutorialRunner(new TutorialInstance(new TutorialId(2), "T", steps));
        }

        public static void Run()
        {
            TestKit.Run("Deferred readiness holds without starting the step", () =>
            {
                var a = new ProbeStep();
                a.SetReadiness(StepReadiness.Deferred);
                var r = Make(Steps.List(a));
                r.Begin();
                r.Tick(0.1f);
                r.Tick(0.1f);
                TestKit.IsFalse(a.BeganCalled, "not begun while deferred");
                TestKit.AreEqual(RunnerPhase.Active, r.Phase, "still active");

                a.SetReadiness(StepReadiness.Ready);
                r.Tick(0.1f);
                TestKit.IsTrue(a.BeganCalled, "begins once ready");
            });

            TestKit.Run("Unreachable readiness aborts the run as Invalidated", () =>
            {
                var a = new ProbeStep();
                a.SetReadiness(StepReadiness.Unreachable);
                var r = Make(Steps.List(a));
                r.Begin();
                r.Tick(0.1f);
                TestKit.AreEqual(TutorialOutcome.Invalidated, r.Outcome.Value, "invalidated");
                TestKit.IsFalse(a.BeganCalled, "never begun");
            });

            TestKit.Run("SkipAndAdvance drops the step and runs the next in the same tick", () =>
            {
                var a = new ProbeStep();
                a.SetReadiness(StepReadiness.SkipAndAdvance);
                var b = new ProbeStep();
                var r = Make(Steps.List(a, b));
                r.Begin();
                r.Tick(0.1f);
                TestKit.IsFalse(a.BeganCalled, "skipped step never begins");
                TestKit.IsTrue(b.BeganCalled, "advanced to next step");
            });

            TestKit.Run("SkipAndAdvance on the last step completes the run", () =>
            {
                var a = new ProbeStep();
                a.SetReadiness(StepReadiness.SkipAndAdvance);
                var r = Make(Steps.List(a));
                r.Begin();
                r.Tick(0.1f);
                TestKit.AreEqual(TutorialOutcome.Completed, r.Outcome.Value, "completed after skipping last");
            });

            TestKit.Run("GoBackOne rewinds to and re-runs the previous step", () =>
            {
                var a = new ProbeStep();
                a.FinishNext();                              // a finishes the instant it advances
                var b = new ProbeStep();
                b.SetReadinessOnce(StepReadiness.GoBackOne); // b rewinds once, then is Ready
                var r = Make(Steps.List(a, b));
                r.Begin();

                r.Tick(0.1f);
                // a runs → finishes → b's readiness says GoBackOne → a re-enters + re-finishes → b now Ready
                // → b begins and waits.
                TestKit.IsTrue(b.BeganCalled, "b eventually began after the rewind");
                TestKit.AreEqual(2, a.ReadinessChecks, "a was entered twice (original + rewind)");
                TestKit.AreEqual(RunnerPhase.Active, r.Phase, "still active, waiting on b");
            });
        }
    }

    internal static class TimeoutCancelTests
    {
        private static TutorialRunner Make(
            IReadOnlyList<ITutorialStep> steps,
            StepTimeoutOutcome defaultOutcome = StepTimeoutOutcome.Advance)
        {
            return new TutorialRunner(new TutorialInstance(new TutorialId(3), "T", steps, defaultOutcome));
        }

        public static void Run()
        {
            TestKit.Run("Timeout cancels the step and advances the run", () =>
            {
                var a = new ProbeStep(timeout: 1.0f); // never self-finishes
                var b = new ProbeStep();
                var r = Make(Steps.List(a, b));
                r.Begin();
                r.Tick(0.5f);
                TestKit.IsFalse(a.CancelCalled, "not yet timed out");
                r.Tick(0.6f); // elapsed 1.1 >= 1.0
                TestKit.IsTrue(a.CancelCalled, "timed out");
                TestKit.AreEqual(StepCancelReason.TimedOut, a.LastCancel, "timeout reason");
                TestKit.IsFalse(a.CompletedCalled, "not completed on timeout");
                TestKit.IsTrue(b.BeganCalled, "advanced to next step after timeout");
            });

            TestKit.Run("Cancelled step is not Completed", () =>
            {
                var a = new ProbeStep();
                var r = Make(Steps.List(a));
                r.Begin();
                r.Tick(0.1f);
                r.Skip();
                TestKit.IsTrue(a.CancelCalled, "cancelled");
                TestKit.IsFalse(a.CompletedCalled, "not completed");
            });

            TestKit.Run("Per-step AbortRun timeout ends the run as Invalidated", () =>
            {
                var a = new ProbeStep(timeout: 1.0f, timeoutOutcome: StepTimeoutOutcome.AbortRun);
                var b = new ProbeStep();
                var r = Make(Steps.List(a, b));
                r.Begin();
                r.Tick(1.1f);
                TestKit.IsTrue(a.CancelCalled, "timed out");
                TestKit.AreEqual(TutorialOutcome.Invalidated, r.Outcome.Value, "run aborted on timeout");
                TestKit.IsFalse(b.BeganCalled, "next step never begins");
            });

            TestKit.Run("Per-tutorial default AbortRun applies when the step doesn't specify", () =>
            {
                var a = new ProbeStep(timeout: 1.0f); // no per-step outcome → inherits tutorial default
                var r = Make(Steps.List(a), defaultOutcome: StepTimeoutOutcome.AbortRun);
                r.Begin();
                r.Tick(1.1f);
                TestKit.AreEqual(TutorialOutcome.Invalidated, r.Outcome.Value, "tutorial default aborted");
            });

            TestKit.Run("Per-step Advance overrides an AbortRun tutorial default", () =>
            {
                var a = new ProbeStep(timeout: 1.0f, timeoutOutcome: StepTimeoutOutcome.Advance);
                var b = new ProbeStep();
                var r = Make(Steps.List(a, b), defaultOutcome: StepTimeoutOutcome.AbortRun);
                r.Begin();
                r.Tick(1.1f);
                TestKit.IsTrue(b.BeganCalled, "step-level Advance beats the tutorial default");
            });
        }
    }
}
