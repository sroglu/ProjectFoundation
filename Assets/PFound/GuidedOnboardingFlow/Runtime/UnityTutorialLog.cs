using PFound.GuidedOnboardingFlow.Core;
using UnityEngine;

namespace PFound.GuidedOnboardingFlow
{
    /// <summary>Routes the core's engine-free log seam to the matching Unity console channel by severity.</summary>
    public sealed class UnityTutorialLog : ITutorialLog
    {
        public void Write(LogSeverity severity, string message)
        {
            string line = "[GuidedOnboarding] " + message;
            switch (severity)
            {
                case LogSeverity.Warning:
                    Debug.LogWarning(line);
                    break;
                case LogSeverity.Error:
                    Debug.LogError(line);
                    break;
                default:
                    Debug.Log(line);
                    break;
            }
        }
    }
}
