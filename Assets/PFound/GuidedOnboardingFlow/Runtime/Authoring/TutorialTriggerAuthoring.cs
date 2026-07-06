using PFound.GuidedOnboardingFlow.Core;
using UnityEngine;

namespace PFound.GuidedOnboardingFlow.Authoring
{
    /// <summary>Authoring base for a trigger asset; produces the runtime <see cref="ITutorialTrigger"/>.</summary>
    public abstract class TutorialTriggerAuthoring : ScriptableObject
    {
        public abstract ITutorialTrigger CreateTrigger();
    }

    [CreateAssetMenu(menuName = "PFound/Guided Onboarding/Triggers/Startup", fileName = "StartupTrigger")]
    public sealed class StartupTriggerAuthoring : TutorialTriggerAuthoring
    {
        public override ITutorialTrigger CreateTrigger() => new StartupTrigger();
    }
}
