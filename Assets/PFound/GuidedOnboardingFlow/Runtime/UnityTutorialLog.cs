using PFound.GuidedOnboardingFlow.Core;
using UnityEngine;

namespace PFound.GuidedOnboardingFlow
{
    /// <summary>Routes the core's engine-free log seam to <see cref="Debug.Log"/>.</summary>
    public sealed class UnityTutorialLog : ITutorialLog
    {
        public void Write(string message) => Debug.Log("[GuidedOnboarding] " + message);
    }
}
