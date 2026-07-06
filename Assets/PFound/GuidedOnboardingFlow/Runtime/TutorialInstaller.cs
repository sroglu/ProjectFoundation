using System.Collections.Generic;
using PFound.GuidedOnboardingFlow.Authoring;
using PFound.GuidedOnboardingFlow.Core;
using PFound.Signaling;
using UnityEngine;
using DiContainer = PFound.DependencyContainer.DependencyContainer;
using RouterService = PFound.ScreenRouter.ScreenRouter;

namespace PFound.GuidedOnboardingFlow
{
    /// <summary>
    /// Scene-level DI bootstrap. Wire the overlay MonoBehaviours + the catalog in the inspector, then call
    /// <see cref="Install"/> from your composition root. It assembles the runtime services, resolves the
    /// catalog into blueprints, builds the manager, binds the tick host, and publishes the manager
    /// (and its collaborators) into the container.
    /// </summary>
    public sealed class TutorialInstaller : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private TutorialCatalog _catalog;

        [Header("Overlay UI")]
        [SerializeField] private TutorialCanvas _canvas;
        [SerializeField] private InputBlocker _inputBlocker;
        [SerializeField] private TutorialHand _hand;
        [SerializeField] private HighlightMask _highlight;
        [SerializeField] private DialogPanel _dialog;
        [SerializeField] private TutorialRunnerHost _host;

        [Header("Anchors present at boot (optional)")]
        [SerializeField] private SceneAnchor[] _anchors;

        /// <summary>
        /// Build and register the tutorial services. Optional router/signals/store let a project opt into
        /// open-screen and wait-for-signal steps and choose a persistence adapter (defaults to in-memory).
        /// </summary>
        public ITutorialManager Install(
            DiContainer container,
            RouterService router = null,
            SignalTracker signals = null,
            ITutorialCompletionStore store = null)
        {
            var anchors = new TutorialAnchorRegistry();
            if (_anchors != null)
            {
                foreach (SceneAnchor anchor in _anchors)
                    anchor.Bind(anchors);
            }

            var services = new TutorialRuntimeServices(
                _inputBlocker,
                _hand,
                _highlight,
                _dialog,
                anchors,
                new UnityTutorialLog(),
                router,
                signals);

            store = store ?? new InMemoryCompletionStore();
            List<TutorialBlueprint> blueprints = _catalog != null
                ? _catalog.BuildBlueprints(services)
                : new List<TutorialBlueprint>();

            var manager = new TutorialManager(blueprints, store);
            services.Attach(manager);

            if (_host != null)
                _host.Bind(manager);

            container.RegisterInstance<ITutorialManager>(manager);
            container.RegisterInstance<ITutorialAnchorRegistry>(anchors);
            container.RegisterInstance(services);

            return manager;
        }
    }
}
