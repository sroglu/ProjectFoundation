using PFound.GuidedOnboardingFlow.Core;
using UnityEngine;

namespace PFound.GuidedOnboardingFlow
{
    /// <summary>
    /// The single MonoBehaviour that drives the engine-free manager: it forwards unscaled frame time each
    /// Update and turns an application quit into a clean <see cref="TutorialOutcome.Shutdown"/> so the
    /// active step tears its affordances down instead of being left dangling.
    /// </summary>
    public sealed class TutorialRunnerHost : MonoBehaviour
    {
        private ITutorialManager _manager;

        public ITutorialManager Manager => _manager;

        public void Bind(ITutorialManager manager) => _manager = manager;

        private void Update()
        {
            _manager?.Tick(Time.unscaledDeltaTime);
        }

        private void OnApplicationQuit()
        {
            if (_manager == null)
                return;
            _manager.Active?.NotifyShutdown();
            _manager.Tick(0f); // retire the shut-down run and fire its ended event
        }
    }
}
