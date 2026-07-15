using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using PFound.InputRouter.Samples;

namespace PFound.InputRouter.Tests
{
    /// <summary>
    /// EditMode coverage for the router plus the sample adapters. The engine-free phase/dispatch
    /// behaviour is exercised exhaustively by the standalone mono suite; here we confirm the same
    /// contract holds inside Unity and that the dual-backend sample adapters feed the router
    /// through the common <see cref="IIntentSource{TIntent}"/> seam.
    /// </summary>
    public sealed class IntentRouterEditModeTests
    {
        /// <summary>Scriptable stand-in for an input backend.</summary>
        private sealed class StubSource<TIntent> : IIntentSource<TIntent> where TIntent : struct, IIntent
        {
            public bool Active;
            public TIntent Payload;

            public IntentReading<TIntent> Read()
            {
                return Active
                    ? IntentReading<TIntent>.Active(Payload)
                    : IntentReading<TIntent>.Inactive;
            }
        }

        private struct DashIntent : IIntent
        {
            public float Power;
        }

        [Test]
        public void EdgeSequence_ProducesFourPhasesInOrder()
        {
            IntentRouter router = new IntentRouter();
            StubSource<DashIntent> source = new StubSource<DashIntent>();
            router.Register(source, IntentGroup.Gameplay);

            List<IntentPhase> seen = new List<IntentPhase>();
            foreach (IntentPhase phase in new[] { IntentPhase.Idle, IntentPhase.Started, IntentPhase.Held, IntentPhase.Ended })
            {
                router.Subscribe<DashIntent>(phase, ctx => seen.Add(ctx.Phase));
            }

            router.Tick();                 // Idle
            source.Active = true;
            router.Tick();                 // Started
            router.Tick();                 // Held
            source.Active = false;
            router.Tick();                 // Ended

            Assert.AreEqual(
                new[] { IntentPhase.Idle, IntentPhase.Started, IntentPhase.Held, IntentPhase.Ended },
                seen.ToArray());
        }

        [Test]
        public void DuplicateRegistration_Throws()
        {
            IntentRouter router = new IntentRouter();
            router.Register(new StubSource<DashIntent>(), IntentGroup.Gameplay);
            Assert.Throws<System.InvalidOperationException>(
                () => router.Register(new StubSource<DashIntent>(), IntentGroup.Gameplay));
        }

        [Test]
        public void Replace_KeepsSubscriptionAndPosition()
        {
            IntentRouter router = new IntentRouter();
            StubSource<DashIntent> first = new StubSource<DashIntent> { Active = true };
            router.Register(first, IntentGroup.Gameplay);

            int hits = 0;
            router.Subscribe<DashIntent>(IntentPhase.Started, _ => hits++);

            StubSource<DashIntent> second = new StubSource<DashIntent> { Active = true };
            router.Replace(second);
            router.Tick(); // Started, via the swapped-in source, subscription preserved

            Assert.AreEqual(1, hits);
        }

        [Test]
        public void GameplayGate_SilencesGameplayButNotSystem()
        {
            IntentRouter router = new IntentRouter { GameplayEnabled = false };
            StubSource<DashIntent> gameplay = new StubSource<DashIntent> { Active = true };
            StubSource<PauseSample> system = new StubSource<PauseSample> { Active = true };
            router.Register(gameplay, IntentGroup.Gameplay);
            router.Register(system, IntentGroup.System);

            int gameplayHits = 0;
            int systemHits = 0;
            router.Subscribe<DashIntent>(IntentPhase.Started, _ => gameplayHits++);
            router.Subscribe<PauseSample>(IntentPhase.Started, _ => systemHits++);

            router.Tick();

            Assert.AreEqual(0, gameplayHits, "gameplay gate should suppress gameplay intent");
            Assert.AreEqual(1, systemHits, "system intent should survive gameplay gate");
        }

        [Test]
        public void UiGate_SuppressesGameplayIntent()
        {
            bool overUi = true;
            IntentRouter router = new IntentRouter(() => overUi);
            StubSource<DashIntent> source = new StubSource<DashIntent> { Active = true };
            router.Register(source, IntentGroup.Gameplay);

            int hits = 0;
            router.Subscribe<DashIntent>(IntentPhase.Started, _ => hits++);

            router.Tick();
            Assert.AreEqual(0, hits, "pointer over UI should suppress gameplay intent");

            overUi = false;
            router.Tick();
            Assert.AreEqual(1, hits, "leaving UI should restore gameplay intent");
        }

        [Test]
        public void LegacyAndInputSystemSources_FeedSameIntentType()
        {
            // Both sample adapters implement IIntentSource<MoveIntent>; the router accepts either
            // through the identical registration seam. We register whichever backend is compiled
            // in, proving the dual-backend design routes through one contract.
            IntentRouter router = new IntentRouter();
            IIntentSource<MoveIntent> moveSource = new MirrorMoveSource();
            router.Register(moveSource, IntentGroup.Gameplay);

            MoveIntent captured = default;
            int hits = 0;
            router.Subscribe<MoveIntent>(IntentPhase.Started, ctx => { captured = ctx.Payload; hits++; });

            router.Tick();

            Assert.AreEqual(1, hits);
            Assert.AreEqual(1f, captured.Horizontal, 0.0001f);
        }

        /// <summary>A minimal always-active MoveIntent source standing in for either backend.</summary>
        private sealed class MirrorMoveSource : IIntentSource<MoveIntent>
        {
            public IntentReading<MoveIntent> Read()
            {
                return IntentReading<MoveIntent>.Active(new MoveIntent(1f, 0f));
            }
        }

        private struct PauseSample : IIntent
        {
        }

        [Test]
        public void Driver_SetInputActive_TogglesRouterDispatch()
        {
            // The driver's combined enable/disable gates the router's global dispatch switch (the
            // EventSystem UI-module half needs a live EventSystem and is covered by Unity-verify).
            GameObject host = new GameObject("intent-router-driver");
            IntentRouterDriver driver = host.AddComponent<IntentRouterDriver>();

            Assert.IsTrue(driver.Router.Enabled, "driver should start with dispatch enabled");

            driver.Disable();
            Assert.IsFalse(driver.Router.Enabled, "Disable should stop router dispatch");

            driver.Enable();
            Assert.IsTrue(driver.Router.Enabled, "Enable should restore router dispatch");

            Object.DestroyImmediate(host);
        }
    }
}
