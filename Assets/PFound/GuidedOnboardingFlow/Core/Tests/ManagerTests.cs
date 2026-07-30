using System.Collections.Generic;

namespace PFound.GuidedOnboardingFlow.Core.Tests
{
    internal static class ManagerTests
    {
        // A blueprint whose most-recently-built step is exposed so tests can drive it.
        private sealed class Fixture
        {
            public ProbeStep Last;
            public TutorialBlueprint Blueprint;

            public Fixture(int id, TriggerMode mode, ITutorialTrigger trigger)
            {
                Blueprint = new TutorialBlueprint(
                    new TutorialId(id), "T" + id, mode,
                    () =>
                    {
                        Last = new ProbeStep();
                        return Steps.List(Last);
                    },
                    trigger);
            }
        }

        public static void Run()
        {
            TestKit.Run("TryStart runs a manual tutorial; single-active blocks a second", () =>
            {
                var f1 = new Fixture(1, TriggerMode.Manual, null);
                var f2 = new Fixture(2, TriggerMode.Manual, null);
                var m = new TutorialManager(new[] { f1.Blueprint, f2.Blueprint }, new InMemoryCompletionStore());

                TestKit.IsTrue(m.TryStart(new TutorialId(1)), "first starts");
                m.Tick(0.1f);
                TestKit.IsFalse(m.TryStart(new TutorialId(2)), "second blocked while active");
                TestKit.IsTrue(m.IsRunning, "still running #1");
            });

            TestKit.Run("Completion is recorded and blocks re-eligibility", () =>
            {
                var store = new InMemoryCompletionStore();
                var f1 = new Fixture(1, TriggerMode.Manual, null);
                var m = new TutorialManager(new[] { f1.Blueprint }, store);

                TutorialOutcome? ended = null;
                m.TutorialEnded += (id, o) => ended = o;

                m.TryStart(new TutorialId(1));
                m.Tick(0.1f);
                f1.Last.FinishNext();
                m.Tick(0.1f);

                TestKit.AreEqual(TutorialOutcome.Completed, ended.Value, "ended completed");
                TestKit.IsTrue(store.IsCompleted(new TutorialId(1)), "recorded");
                TestKit.IsFalse(m.IsRunning, "idle again");
            });

            TestKit.Run("Automatic startup triggers fire; registration order breaks ties", () =>
            {
                var f1 = new Fixture(1, TriggerMode.Automatic, new StartupTrigger());
                var f2 = new Fixture(2, TriggerMode.Automatic, new StartupTrigger());
                var m = new TutorialManager(new[] { f1.Blueprint, f2.Blueprint }, new InMemoryCompletionStore());

                TutorialId started = TutorialId.None;
                m.TutorialStarted += id => { if (started.IsNone) started = id; };

                m.Tick(0.1f);
                TestKit.AreEqual(1, started.handle, "first-registered wins the tie");
                TestKit.IsTrue(m.IsRunning, "running the winner");
            });

            TestKit.Run("Whitelist restricts automatic eligibility", () =>
            {
                var f1 = new Fixture(1, TriggerMode.Automatic, new StartupTrigger());
                var f2 = new Fixture(2, TriggerMode.Automatic, new StartupTrigger());
                var m = new TutorialManager(new[] { f1.Blueprint, f2.Blueprint }, new InMemoryCompletionStore());
                m.SetRunSpecific(new[] { new TutorialId(2) });

                TutorialId started = TutorialId.None;
                m.TutorialStarted += id => { if (started.IsNone) started = id; };
                m.Tick(0.1f);
                TestKit.AreEqual(2, started.handle, "only whitelisted #2 runs");
            });

            TestKit.Run("Skip ends the active run and frees the manager", () =>
            {
                var f1 = new Fixture(1, TriggerMode.Manual, null);
                var m = new TutorialManager(new[] { f1.Blueprint }, new InMemoryCompletionStore());
                TutorialOutcome? ended = null;
                m.TutorialEnded += (id, o) => ended = o;

                m.TryStart(new TutorialId(1));
                m.Tick(0.1f);
                m.Skip();
                TestKit.AreEqual(TutorialOutcome.Skipped, ended.Value, "skipped");
                TestKit.IsFalse(m.IsRunning, "freed");
            });

            TestKit.Run("ForceStart aborts the current run and starts the new one", () =>
            {
                var f1 = new Fixture(1, TriggerMode.Manual, null);
                var f2 = new Fixture(2, TriggerMode.Manual, null);
                var m = new TutorialManager(new[] { f1.Blueprint, f2.Blueprint }, new InMemoryCompletionStore());
                var ends = new List<TutorialOutcome>();
                m.TutorialEnded += (id, o) => ends.Add(o);

                m.TryStart(new TutorialId(1));
                m.Tick(0.1f);
                m.ForceStart(new TutorialId(2));
                TestKit.AreEqual(TutorialOutcome.Shutdown, ends[0], "old aborted as shutdown");
                TestKit.IsTrue(m.IsRunning, "new one active");
                TestKit.AreEqual(2, m.Active.Id.handle, "active is #2");
            });
        }
    }
}
