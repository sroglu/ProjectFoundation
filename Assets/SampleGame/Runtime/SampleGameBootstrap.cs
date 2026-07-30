using System;
using System.Threading;
using PFound.EpochClock;
using PFound.SampleGame.Steps;
using PFound.Signaling;
using PFound.StartupOrchestration;
using UnityEngine;
using DIContainer = PFound.DependencyContainer.DependencyContainer;
using Router = PFound.ScreenRouter.ScreenRouter;
using Loop = PFound.LoopScheduler.LoopScheduler;

namespace PFound.SampleGame
{
    /// <summary>
    /// The runnable reference for wiring the PFound boot modules together. On play it builds a
    /// <c>DependencyContainer</c> holding a <see cref="ClientEpochClock"/> and a
    /// <see cref="SignalTracker"/>, drives the clock from <c>LoopScheduler</c>, runs a couple
    /// of demo <see cref="IStartupStep"/>s through a <see cref="StartupOrchestrator"/>, and on
    /// completion emits <see cref="BootCompleted"/> — optionally routing to <see cref="SampleScreen"/>
    /// when a <c>ScreenRouterHost</c> is present. Thin MonoBehaviour shell: it only owns Unity
    /// lifecycle; the collaborators are plain C#.
    /// </summary>
    public sealed class SampleGameBootstrap : MonoBehaviour
    {
        private DIContainer _container;
        private ClientEpochClock _clock;
        private SignalTracker _signals;
        private StartupOrchestrator _orchestrator;
        private Action _tickLoop;
        private CancellationTokenSource _lifetimeCts;
        private bool _containerBuilt;

        private void Awake()
        {
            _lifetimeCts = new CancellationTokenSource();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SampleKunaiBootstrap.Initialize(); // debug overlay: toggle with ` / F1 or 3-tap top-left
#endif
        }

        private async void Start()
        {
            _container = new DIContainer();
            _container.RegisterInstance(new ClientEpochClock());
            _container.RegisterInstance(new SignalTracker());
            _container.Build();
            _containerBuilt = true;

            _clock = _container.Get<ClientEpochClock>();
            _signals = _container.Get<SignalTracker>();

            // Advance the game clock once per frame through the shared player-loop scheduler.
            _tickLoop = () => _clock.Tick();
            Loop.RegisterUpdateLoop(_tickLoop, this);

            _signals.AddListener<BootCompleted>(OnBootCompleted);

            _orchestrator = new StartupOrchestrator();
            _orchestrator.Register(new DelayStartupStep(WaitReason.Catalog, weight: 1f, totalDelayMs: 120));
            _orchestrator.Register(new DelayStartupStep(WaitReason.Translations, weight: 1f, totalDelayMs: 80));
            _orchestrator.Register(new DelayStartupStep(WaitReason.Systems, weight: 2f, totalDelayMs: 60));

            var progress = new Progress<StartupAggregate>(OnStartupProgress);
            await _orchestrator.RunAsync(progress, _lifetimeCts.Token);

            Debug.Log($"[SampleGame] Boot pipeline complete at unix {_clock.Now.UnixSeconds:F0}s.");
            _signals.Queue<BootCompleted>(this);
            _signals.EmitQueuedSignals();
        }

        private void OnStartupProgress(StartupAggregate aggregate) =>
            Debug.Log($"[SampleGame] Boot {aggregate.Overall01:P0} — waiting on {aggregate.DominantReason}.");

        private void OnBootCompleted(SignalKey key)
        {
            Debug.Log("[SampleGame] PFound Sample Game — booted.");

            // Routing is optional: the scene runs without any authored screen prefabs. Only switch
            // when a ScreenRouterHost has published a router onto the static seam.
            if (Router.HasInstance)
            {
                Router.Instance.SwitchScreen<SampleScreen>();
                return;
            }

            Debug.Log("[SampleGame] No ScreenRouterHost in scene — skipping screen switch. Add a host " +
                      "plus a SampleScreen prefab/definition to see routing.");
        }

        private void OnDestroy()
        {
            if (_containerBuilt)
            {
                Loop.DeregisterUpdateLoop(_tickLoop);
                _container.Dispose();
            }

            _lifetimeCts.Cancel();
            _lifetimeCts.Dispose();
        }
    }
}
