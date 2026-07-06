using PFound.GuidedOnboardingFlow.Core;
using UnityEngine;
using DiContainer = PFound.DependencyContainer.DependencyContainer;

namespace PFound.GuidedOnboardingFlow.Sample
{
    /// <summary>
    /// Minimal end-to-end sample: builds a container, installs the tutorial services from a scene-wired
    /// <see cref="TutorialInstaller"/> + catalog, then exposes simple keyboard controls — start the first
    /// catalog tutorial, skip the active one, and toggle auto-advance. Logs every lifecycle event so the
    /// flow is visible in the console.
    /// </summary>
    public sealed class SampleOnboardingBootstrap : MonoBehaviour
    {
        [SerializeField] private TutorialInstaller _installer;
        [SerializeField] private int _firstTutorialHandle = 1;

        private ITutorialManager _manager;

        private void Start()
        {
            var container = new DiContainer();
            _manager = _installer.Install(container);

            _manager.TutorialStarted += id => Debug.Log("[Sample] started " + id);
            _manager.StepStarted += step => Debug.Log("[Sample] step " + step.GetType().Name);
            _manager.TutorialEnded += (id, outcome) => Debug.Log("[Sample] ended " + id + " -> " + outcome);
        }

        private void Update()
        {
            if (_manager == null)
                return;

            if (Input.GetKeyDown(KeyCode.T))
                _manager.TryStart(new TutorialId(_firstTutorialHandle));
            if (Input.GetKeyDown(KeyCode.S))
                _manager.Skip();
            if (Input.GetKeyDown(KeyCode.A))
                _manager.AutoAdvance = !_manager.AutoAdvance;
        }
    }
}
