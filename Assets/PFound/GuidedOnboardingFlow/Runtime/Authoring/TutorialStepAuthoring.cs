using PFound.GuidedOnboardingFlow.Core;
using UnityEngine;

namespace PFound.GuidedOnboardingFlow.Authoring
{
    /// <summary>
    /// Authoring base: a ScriptableObject that knows how to produce a fresh runtime <see cref="ITutorialStep"/>
    /// from the resolved services. Concrete step assets are added as sub-assets of a definition; subclass
    /// this to add new authorable step kinds. Every step exposes an optional per-step timeout and, when
    /// that timeout fires, whether the run advances past the step or aborts (or defers to the tutorial's
    /// default).
    /// </summary>
    public abstract class TutorialStepAuthoring : ScriptableObject
    {
        [Tooltip("Optional per-step time budget in seconds; 0 = no limit.")]
        [SerializeField] protected float _timeoutSeconds;

        [Tooltip("Use the tutorial default, or force Advance / AbortRun when this step times out.")]
        [SerializeField] protected TimeoutOutcomeOption _onTimeout = TimeoutOutcomeOption.UseTutorialDefault;

        protected float? Timeout => _timeoutSeconds > 0f ? _timeoutSeconds : (float?)null;

        protected StepTimeoutOutcome? TimeoutOutcome
        {
            get
            {
                switch (_onTimeout)
                {
                    case TimeoutOutcomeOption.Advance: return StepTimeoutOutcome.Advance;
                    case TimeoutOutcomeOption.AbortRun: return StepTimeoutOutcome.AbortRun;
                    default: return null;
                }
            }
        }

        public abstract ITutorialStep CreateStep(TutorialRuntimeServices services);

        public enum TimeoutOutcomeOption
        {
            UseTutorialDefault,
            Advance,
            AbortRun
        }
    }

    [CreateAssetMenu(menuName = "PFound/Guided Onboarding/Steps/Log", fileName = "LogStep")]
    public sealed class LogStepAuthoring : TutorialStepAuthoring
    {
        [SerializeField] [TextArea] private string _message = "…";
        [SerializeField] private LogSeverity _severity = LogSeverity.Info;

        public override ITutorialStep CreateStep(TutorialRuntimeServices services)
        {
            return new LogStep(services.Log, _message, _severity);
        }
    }

    [CreateAssetMenu(menuName = "PFound/Guided Onboarding/Steps/Focus", fileName = "FocusStep")]
    public sealed class FocusStepAuthoring : TutorialStepAuthoring
    {
        [Tooltip("Anchor tag of the UI element to spotlight (falls back to GameObject.Find by this name).")]
        [SerializeField] private string _anchorTag;
        [Tooltip("Optional hint caption shown beside the hand pointer.")]
        [SerializeField] private string _hint;
        [Tooltip("Optional mask sprite name; an anchor's own mask overrides this when set.")]
        [SerializeField] private string _maskSpriteName;

        public override ITutorialStep CreateStep(TutorialRuntimeServices services)
        {
            return new FocusStep(services, _anchorTag, _hint, _maskSpriteName, Timeout, TimeoutOutcome);
        }
    }

    [CreateAssetMenu(menuName = "PFound/Guided Onboarding/Steps/Dialog", fileName = "DialogStep")]
    public sealed class DialogStepAuthoring : TutorialStepAuthoring
    {
        [SerializeField] [TextArea] private string _message = "…";
        [Tooltip("Typewriter reveal speed in characters/second; 0 = instant.")]
        [SerializeField] private float _typingSpeedCps;
        [SerializeField] private DialogDismissMode _dismissMode = DialogDismissMode.Tap;
        [Tooltip("Seconds the fully-revealed line stays before auto-dismissing (Auto / Both modes).")]
        [SerializeField] private float _autoDismissSeconds = 2f;

        public override ITutorialStep CreateStep(TutorialRuntimeServices services)
        {
            float? autoDismiss = _dismissMode == DialogDismissMode.Tap ? (float?)null : _autoDismissSeconds;
            return new DialogStep(services, _message, _typingSpeedCps, _dismissMode, autoDismiss, Timeout, TimeoutOutcome);
        }
    }
}
