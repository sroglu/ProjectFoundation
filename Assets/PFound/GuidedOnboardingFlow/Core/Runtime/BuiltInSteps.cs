using System;

namespace PFound.GuidedOnboardingFlow.Core
{
    /// <summary>Engine-free logging seam so the core's diagnostic step needn't reference UnityEngine.Debug.</summary>
    public interface ITutorialLog
    {
        void Write(LogSeverity severity, string message);
    }

    /// <summary>
    /// Emits a single line through <see cref="ITutorialLog"/> at a chosen <see cref="LogSeverity"/> then
    /// finishes immediately. The simplest possible step, handy for authoring checkpoints (info) and for
    /// surfacing "you're doing it wrong" hints (warning) during a run, plus exercising the runner in tests.
    /// </summary>
    public sealed class LogStep : TutorialStepBase
    {
        private readonly ITutorialLog _log;
        private readonly string _message;
        private readonly LogSeverity _severity;

        public LogStep(ITutorialLog log, string message, LogSeverity severity = LogSeverity.Info)
        {
            _log = log;
            _message = message;
            _severity = severity;
        }

        protected override void OnBegin()
        {
            _log?.Write(_severity, _message);
        }

        protected override StepStatus OnAdvance(float deltaSeconds) => StepStatus.Finished;
    }

    /// <summary>
    /// Holds for a fixed duration then finishes. Because the base class already tracks elapsed time this
    /// step is a thin comparison; it also doubles as a pacing beat between visual steps.
    /// </summary>
    public sealed class DelayStep : TutorialStepBase
    {
        private readonly float _seconds;

        public DelayStep(float seconds)
        {
            _seconds = Math.Max(0f, seconds);
        }

        protected override void OnBegin()
        {
        }

        protected override StepStatus OnAdvance(float deltaSeconds)
        {
            return Elapsed >= _seconds ? StepStatus.Finished : StepStatus.Running;
        }
    }
}
