namespace PFound.GuidedOnboardingFlow.Core.Tests
{
    internal static class PersistenceTests
    {
        public static void Run()
        {
            TestKit.Run("InMemory store hydrates then records and clears", () =>
            {
                var s = new InMemoryCompletionStore();
                s.HydrateAsync().GetAwaiter().GetResult();
                TestKit.IsFalse(s.IsCompleted(new TutorialId(7)), "initially clear");
                s.MarkCompleted(new TutorialId(7));
                TestKit.IsTrue(s.IsCompleted(new TutorialId(7)), "recorded");
                s.Clear(new TutorialId(7));
                TestKit.IsFalse(s.IsCompleted(new TutorialId(7)), "cleared");
            });

            TestKit.Run("Session store keeps state until the session resets", () =>
            {
                var s = new SessionCompletionStore();
                s.HydrateAsync().GetAwaiter().GetResult();
                s.MarkCompleted(new TutorialId(4));
                int token = s.SessionToken;
                TestKit.IsTrue(s.IsCompleted(new TutorialId(4)), "held in session");

                s.ResetSession();
                TestKit.IsFalse(s.IsCompleted(new TutorialId(4)), "dropped on new session");
                TestKit.IsTrue(s.SessionToken != token, "token advanced");
            });
        }
    }

    internal static class BuiltInStepTests
    {
        public static void Run()
        {
            TestKit.Run("LogStep writes its line then finishes", () =>
            {
                var log = new RecordingLog();
                var step = new LogStep(log, "hello");
                step.Begin();
                var status = step.Advance(0f);
                TestKit.AreEqual(StepStatus.Finished, status, "finished");
                TestKit.AreEqual(1, log.Lines.Count, "one line");
                TestKit.AreEqual("hello", log.Lines[0], "line text");
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
