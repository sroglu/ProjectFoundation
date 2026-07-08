using System.Collections.Generic;

namespace PFound.GuidedOnboardingFlow.Core.Tests
{
    internal static class PersistenceTests
    {
        private static bool Contains(IReadOnlyCollection<TutorialId> all, int handle)
        {
            foreach (TutorialId id in all)
                if (id.handle == handle)
                    return true;
            return false;
        }

        public static void Run()
        {
            TestKit.Run("InMemory store hydrates then records and clears", () =>
            {
                var s = new InMemoryCompletionStore();
                s.HydrateAsync().GetAwaiter().GetResult();
                TestKit.IsFalse(s.IsCompleted(new TutorialId(7)), "initially clear");
                s.MarkCompleted(new TutorialId(7), ReplayPolicy.OnceAccount);
                TestKit.IsTrue(s.IsCompleted(new TutorialId(7)), "recorded");
                s.Clear(new TutorialId(7));
                TestKit.IsFalse(s.IsCompleted(new TutorialId(7)), "cleared");
            });

            TestKit.Run("Repeatable policy is never recorded", () =>
            {
                var s = new InMemoryCompletionStore();
                s.MarkCompleted(new TutorialId(9), ReplayPolicy.Repeatable);
                TestKit.IsFalse(s.IsCompleted(new TutorialId(9)), "repeatable not remembered");
            });

            TestKit.Run("GetAllCompleted enumerates recorded ids", () =>
            {
                var s = new InMemoryCompletionStore();
                s.MarkCompleted(new TutorialId(1), ReplayPolicy.OnceAccount);
                s.MarkCompleted(new TutorialId(2), ReplayPolicy.OnceSession);
                var all = s.GetAllCompleted();
                TestKit.AreEqual(2, all.Count, "two recorded");
                TestKit.IsTrue(Contains(all, 1) && Contains(all, 2), "both present");
            });

            TestKit.Run("Session store keeps state until the session resets", () =>
            {
                var s = new SessionCompletionStore();
                s.HydrateAsync().GetAwaiter().GetResult();
                s.MarkCompleted(new TutorialId(4), ReplayPolicy.OnceSession);
                int token = s.SessionToken;
                TestKit.IsTrue(s.IsCompleted(new TutorialId(4)), "held in session");

                s.ResetSession();
                TestKit.IsFalse(s.IsCompleted(new TutorialId(4)), "dropped on new session");
                TestKit.IsTrue(s.SessionToken != token, "token advanced");
            });

            TestKit.Run("Composite routes OnceAccount to persistent, OnceSession to session tier", () =>
            {
                var persistent = new InMemoryCompletionStore();
                var composite = new CompositeCompletionStore(persistent);

                composite.MarkCompleted(new TutorialId(10), ReplayPolicy.OnceAccount);
                composite.MarkCompleted(new TutorialId(11), ReplayPolicy.OnceSession);
                composite.MarkCompleted(new TutorialId(12), ReplayPolicy.Repeatable);

                TestKit.IsTrue(persistent.IsCompleted(new TutorialId(10)), "account tier persisted");
                TestKit.IsFalse(persistent.IsCompleted(new TutorialId(11)), "session id not in persistent tier");
                TestKit.IsTrue(composite.IsCompleted(new TutorialId(11)), "session id visible via composite");
                TestKit.IsFalse(composite.IsCompleted(new TutorialId(12)), "repeatable never recorded");

                var all = composite.GetAllCompleted();
                TestKit.AreEqual(2, all.Count, "union of both tiers");
                TestKit.IsTrue(Contains(all, 10) && Contains(all, 11), "both tiers unioned");
            });
        }
    }

    internal static class BuiltInStepTests
    {
        public static void Run()
        {
            TestKit.Run("LogStep writes its line at the chosen severity then finishes", () =>
            {
                var log = new RecordingLog();
                var step = new LogStep(log, "hello", LogSeverity.Warning);
                step.Begin();
                var status = step.Advance(0f);
                TestKit.AreEqual(StepStatus.Finished, status, "finished");
                TestKit.AreEqual(1, log.Lines.Count, "one line");
                TestKit.AreEqual("hello", log.Lines[0], "line text");
                TestKit.AreEqual(LogSeverity.Warning, log.Severities[0], "severity carried");
            });

            TestKit.Run("DelayStep runs until its duration elapses", () =>
            {
                var step = new DelayStep(1.0f);
                step.Begin();
                TestKit.AreEqual(StepStatus.Running, step.Advance(0.4f), "still running");
                TestKit.AreEqual(StepStatus.Finished, step.Advance(0.7f), "finished after 1.1s");
            });
        }
    }
}
