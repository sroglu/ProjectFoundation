using System;
using System.Collections.Generic;

namespace PFound.InputRouter.Tests
{
    /// <summary>
    /// Standalone entry point for the engine-free IntentRouter suite. Runs under mono/csc with no
    /// Unity and no NUnit; exit code 0 = all green.
    /// </summary>
    internal static class Program
    {
        public static int Main()
        {
            PhaseMachineTests();
            SubscriptionTests();
            RegistrationTests();
            DispatchOrderTests();
            ReplaceAndUnregisterTests();
            GatingTests();
            UiGateTests();
            return Assert.Report("PFound.InputRouter.Core");
        }

        // --- 1. Four-phase edge detection across ticks -------------------------------------

        private static void PhaseMachineTests()
        {
            IntentRouter router = new IntentRouter();
            FakeSource<FireIntent> source = new FakeSource<FireIntent>();
            router.Register(source, IntentGroup.Gameplay);

            PhaseLog<FireIntent> log = new PhaseLog<FireIntent>();
            foreach (IntentPhase phase in AllPhases())
            {
                IntentPhase captured = phase;
                router.Subscribe<FireIntent>(phase, ctx => log.Record(ctx));
            }

            // Inactive -> Idle.
            router.Tick();
            Assert.Equal(IntentPhase.Idle, log.Last, "phase: inactive tick is Idle");

            // Rising edge -> Started, payload flows through.
            source.Active = true;
            source.Payload = new FireIntent { Strength = 0.5f };
            router.Tick();
            Assert.Equal(IntentPhase.Started, log.Last, "phase: rising edge is Started");
            Assert.Equal(0.5f, log.Payloads[log.Count - 1].Strength, "phase: payload delivered on Started");

            // Still active -> Held.
            router.Tick();
            Assert.Equal(IntentPhase.Held, log.Last, "phase: sustained is Held");

            // Falling edge -> Ended.
            source.Active = false;
            router.Tick();
            Assert.Equal(IntentPhase.Ended, log.Last, "phase: falling edge is Ended");

            // Back to Idle.
            router.Tick();
            Assert.Equal(IntentPhase.Idle, log.Last, "phase: after release is Idle");
        }

        // --- 2. Subscriptions: only bound phase fires; unbind stops delivery ---------------

        private static void SubscriptionTests()
        {
            IntentRouter router = new IntentRouter();
            FakeSource<FireIntent> source = new FakeSource<FireIntent>();
            router.Register(source, IntentGroup.Gameplay);

            int startedCount = 0;
            int endedCount = 0;
            IDisposable startedSub = router.Subscribe<FireIntent>(IntentPhase.Started, _ => startedCount++);
            router.Subscribe<FireIntent>(IntentPhase.Ended, _ => endedCount++);

            source.Active = true;
            router.Tick(); // Started
            router.Tick(); // Held  -> neither handler
            source.Active = false;
            router.Tick(); // Ended

            Assert.Equal(1, startedCount, "subscribe: Started fired exactly once");
            Assert.Equal(1, endedCount, "subscribe: Ended fired exactly once");

            // Unbind Started, then press again: only Ended should move.
            startedSub.Dispose();
            source.Active = true;
            router.Tick(); // Started (no listener now)
            source.Active = false;
            router.Tick(); // Ended
            Assert.Equal(1, startedCount, "unbind: disposed Started no longer fires");
            Assert.Equal(2, endedCount, "unbind: Ended still fires after unrelated unbind");
        }

        // --- 3. Register fail-fast + poll-once-per-tick ------------------------------------

        private static void RegistrationTests()
        {
            IntentRouter router = new IntentRouter();
            FakeSource<FireIntent> source = new FakeSource<FireIntent>();
            router.Register(source, IntentGroup.Gameplay);

            Assert.That(router.IsRegistered<FireIntent>(), "register: IsRegistered true after Register");
            Assert.That(!router.IsRegistered<JumpIntent>(), "register: IsRegistered false for unregistered");

            Assert.Rejects<InvalidOperationException>(
                () => router.Register(new FakeSource<FireIntent>(), IntentGroup.Gameplay),
                "register: duplicate registration fails fast");

            Assert.Rejects<InvalidOperationException>(
                () => router.Replace(new FakeSource<JumpIntent>()),
                "register: replace of missing intent fails fast");

            Assert.Rejects<InvalidOperationException>(
                () => router.Unregister<JumpIntent>(),
                "register: unregister of missing intent fails fast");

            Assert.Rejects<InvalidOperationException>(
                () => router.Subscribe<JumpIntent>(IntentPhase.Started, _ => { }),
                "register: subscribe before register fails fast");

            router.Tick();
            router.Tick();
            Assert.Equal(2, source.Polls, "register: source polled once per tick");
        }

        // --- 4. Deterministic registration-order dispatch ---------------------------------

        private static void DispatchOrderTests()
        {
            IntentRouter router = new IntentRouter();
            List<string> order = new List<string>();

            FakeSource<FireIntent> fire = new FakeSource<FireIntent> { Active = true };
            FakeSource<JumpIntent> jump = new FakeSource<JumpIntent> { Active = true };
            FakeSource<PauseIntent> pause = new FakeSource<PauseIntent> { Active = true };

            router.Register(fire, IntentGroup.Gameplay);
            router.Register(jump, IntentGroup.Gameplay);
            router.Register(pause, IntentGroup.System);

            router.Subscribe<FireIntent>(IntentPhase.Started, _ => order.Add("fire"));
            router.Subscribe<JumpIntent>(IntentPhase.Started, _ => order.Add("jump"));
            router.Subscribe<PauseIntent>(IntentPhase.Started, _ => order.Add("pause"));

            router.Tick();
            Assert.Equal("fire,jump,pause", string.Join(",", order.ToArray()),
                "order: dispatch follows registration order");
        }

        // --- 5. Replace preserves subs + slot; unregister resets state --------------------

        private static void ReplaceAndUnregisterTests()
        {
            IntentRouter router = new IntentRouter();
            List<string> order = new List<string>();

            FakeSource<FireIntent> fire = new FakeSource<FireIntent> { Active = true };
            FakeSource<JumpIntent> jump = new FakeSource<JumpIntent> { Active = true };
            router.Register(fire, IntentGroup.Gameplay);
            router.Register(jump, IntentGroup.Gameplay);

            // Track the sustained phase so an intent held active logs every tick it survives.
            router.Subscribe<FireIntent>(IntentPhase.Held, _ => order.Add("fire"));
            router.Subscribe<JumpIntent>(IntentPhase.Held, _ => order.Add("jump"));

            // Prime both to active (Started this tick, Held from the next on).
            router.Tick();
            order.Clear();

            // Replace fire's source with a fresh one; subscription + first slot must survive.
            FakeSource<FireIntent> fire2 = new FakeSource<FireIntent> { Active = true };
            router.Replace(fire2);
            router.Tick();
            Assert.Equal("fire,jump", string.Join(",", order.ToArray()),
                "replace: keeps subscription and dispatch position");
            Assert.That(fire2.Polls == 1 && fire.Polls == 1,
                "replace: new source takes over polling from the next tick");

            // Unregister jump: it stops dispatching, order slot collapses cleanly.
            order.Clear();
            router.Unregister<JumpIntent>();
            router.Tick();
            Assert.Equal("fire", string.Join(",", order.ToArray()),
                "unregister: removed intent no longer dispatches");

            // Re-register jump: phase state is fresh, so a held input reads as a new rising edge.
            List<IntentPhase> rePhases = new List<IntentPhase>();
            FakeSource<JumpIntent> jump2 = new FakeSource<JumpIntent> { Active = true };
            router.Register(jump2, IntentGroup.Gameplay);
            router.Subscribe<JumpIntent>(IntentPhase.Started, ctx => rePhases.Add(ctx.Phase));
            router.Tick();
            Assert.Equal(1, rePhases.Count,
                "unregister: re-registered intent starts from a clean edge (fresh Started)");
        }

        // --- 6. Group + global gating -----------------------------------------------------

        private static void GatingTests()
        {
            IntentRouter router = new IntentRouter();
            FakeSource<FireIntent> fire = new FakeSource<FireIntent> { Active = true };
            FakeSource<PauseIntent> pause = new FakeSource<PauseIntent> { Active = true };
            router.Register(fire, IntentGroup.Gameplay);
            router.Register(pause, IntentGroup.System);

            int fireStarted = 0;
            int pauseStarted = 0;
            router.Subscribe<FireIntent>(IntentPhase.Started, _ => fireStarted++);
            router.Subscribe<PauseIntent>(IntentPhase.Started, _ => pauseStarted++);

            // Gameplay off: gameplay intent silenced, system intent still fires.
            router.GameplayEnabled = false;
            router.Tick();
            Assert.Equal(0, fireStarted, "gate: gameplay disabled suppresses gameplay intent");
            Assert.Equal(1, pauseStarted, "gate: gameplay disabled leaves system intent alive");

            // Re-enable gameplay: held input now reads as a fresh rising edge.
            router.GameplayEnabled = true;
            router.Tick();
            Assert.Equal(1, fireStarted, "gate: re-enabling gameplay yields a fresh Started");

            // Global off: nothing fires, not even system.
            pauseStarted = 0;
            router.Enabled = false;
            router.Tick();
            Assert.Equal(0, pauseStarted, "gate: global disable suppresses every group");
        }

        // --- 7. UI-over-pointer gate suppresses gameplay only -----------------------------

        private static void UiGateTests()
        {
            bool pointerOverUi = false;
            IntentRouter router = new IntentRouter(() => pointerOverUi);

            FakeSource<FireIntent> fire = new FakeSource<FireIntent> { Active = true };
            FakeSource<PauseIntent> pause = new FakeSource<PauseIntent> { Active = true };
            router.Register(fire, IntentGroup.Gameplay);
            router.Register(pause, IntentGroup.System);

            int fireStarted = 0;
            int pauseStarted = 0;
            router.Subscribe<FireIntent>(IntentPhase.Started, _ => fireStarted++);
            router.Subscribe<PauseIntent>(IntentPhase.Started, _ => pauseStarted++);

            pointerOverUi = true;
            router.Tick();
            Assert.Equal(0, fireStarted, "ui-gate: pointer over UI suppresses gameplay intent");
            Assert.Equal(1, pauseStarted, "ui-gate: pointer over UI leaves system intent alive");

            pointerOverUi = false;
            router.Tick();
            Assert.Equal(1, fireStarted, "ui-gate: leaving UI restores gameplay intent (fresh edge)");
        }

        private static IEnumerable<IntentPhase> AllPhases()
        {
            yield return IntentPhase.Idle;
            yield return IntentPhase.Started;
            yield return IntentPhase.Held;
            yield return IntentPhase.Ended;
        }
    }
}
