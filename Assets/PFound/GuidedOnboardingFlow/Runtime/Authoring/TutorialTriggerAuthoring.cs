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

    [CreateAssetMenu(menuName = "PFound/Guided Onboarding/Triggers/Delayed Startup", fileName = "DelayedStartupTrigger")]
    public sealed class DelayedStartupTriggerAuthoring : TutorialTriggerAuthoring
    {
        [Tooltip("Fire once the manager has been running for at least this many seconds.")]
        [SerializeField] private float _afterSeconds = 3f;

        public override ITutorialTrigger CreateTrigger() => new DelayedStartupTrigger(_afterSeconds);
    }
}
