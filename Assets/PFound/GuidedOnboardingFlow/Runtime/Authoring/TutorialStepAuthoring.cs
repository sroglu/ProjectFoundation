using PFound.GuidedOnboardingFlow.Core;
using UnityEngine;

namespace PFound.GuidedOnboardingFlow.Authoring
{
    /// <summary>
    /// Authoring base: a ScriptableObject that knows how to produce a fresh runtime <see cref="ITutorialStep"/>
    /// from the resolved services. Concrete step assets are added as sub-assets of a definition; subclass
    /// this to add new authorable step kinds.
    /// </summary>
    public abstract class TutorialStepAuthoring : ScriptableObject
    {
        [Tooltip("Optional per-step time budget in seconds; 0 = no limit.")]
        [SerializeField] protected float _timeoutSeconds;

        protected float? Timeout => _timeoutSeconds > 0f ? _timeoutSeconds : (float?)null;

        public abstract ITutorialStep CreateStep(TutorialRuntimeServices services);
    }

    [CreateAssetMenu(menuName = "PFound/Guided Onboarding/Steps/Log", fileName = "LogStep")]
    public sealed class LogStepAuthoring : TutorialStepAuthoring
    {
        [SerializeField] [TextArea] private string _message = "…";

        public override ITutorialStep CreateStep(TutorialRuntimeServices services)
        {
            return new LogStep(services.Log, _message);
        }
    }

    [CreateAssetMenu(menuName = "PFound/Guided Onboarding/Steps/Focus", fileName = "FocusStep")]
    public sealed class FocusStepAuthoring : TutorialStepAuthoring
    {
        [Tooltip("Anchor tag of the UI element to spotlight.")]
        [SerializeField] private string _anchorTag;

        public override ITutorialStep CreateStep(TutorialRuntimeServices services)
        {
            return new FocusStep(services, _anchorTag, Timeout);
        }
    }

    [CreateAssetMenu(menuName = "PFound/Guided Onboarding/Steps/Dialog", fileName = "DialogStep")]
    public sealed class DialogStepAuthoring : TutorialStepAuthoring
    {
        [SerializeField] [TextArea] private string _message = "…";

        public override ITutorialStep CreateStep(TutorialRuntimeServices services)
        {
            return new DialogStep(services, _message, Timeout);
        }
    }
}
