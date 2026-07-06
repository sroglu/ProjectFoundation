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
        }
    }

    internal static class TimeoutCancelTests
    {
        private static TutorialRunner Make(IReadOnlyList<ITutorialStep> steps)
        {
            return new TutorialRunner(new TutorialInstance(new TutorialId(3), "T", steps));
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
        }
    }
}
