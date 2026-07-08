using System;
using System.Collections.Generic;

namespace PFound.GuidedOnboardingFlow.Core.Tests
{
    /// <summary>A step whose completion, readiness and faulting are driven by the test.</summary>
    internal sealed class ProbeStep : TutorialStepBase
    {
        public bool BeganCalled;
        public bool CompletedCalled;
        public bool CancelCalled;
        public StepCancelReason LastCancel;

        public int ReadinessChecks;

        private bool _finishNow;
        private StepReadiness _readiness = StepReadiness.Ready;
        private StepReadiness? _readinessOnce;
        private readonly bool _throwOnBegin;
        private readonly bool _throwOnAdvance;

        public ProbeStep(
            float? timeout = null,
            bool throwOnBegin = false,
            bool throwOnAdvance = false,
            StepTimeoutOutcome? timeoutOutcome = null)
        {
            Timeout = timeout;
            TimeoutOutcome = timeoutOutcome;
            _throwOnBegin = throwOnBegin;
            _throwOnAdvance = throwOnAdvance;
        }

        public void FinishNext() => _finishNow = true;

        public void SetReadiness(StepReadiness readiness) => _readiness = readiness;

        /// <summary>Return <paramref name="readiness"/> on the next check only, then revert to the steady value.</summary>
        public void SetReadinessOnce(StepReadiness readiness) => _readinessOnce = readiness;

        protected override StepReadiness OnCheckReadiness()
        {
            ReadinessChecks++;
            if (_readinessOnce.HasValue)
            {
                StepReadiness once = _readinessOnce.Value;
                _readinessOnce = null;
                return once;
            }
            return _readiness;
        }

        protected override void OnBegin()
        {
            BeganCalled = true;
            if (_throwOnBegin)
                throw new InvalidOperationException("begin blew up");
        }

        protected override StepStatus OnAdvance(float deltaSeconds)
        {
            if (_throwOnAdvance)
                throw new InvalidOperationException("advance blew up");
            return _finishNow ? StepStatus.Finished : StepStatus.Running;
        }

        protected override void OnComplete() => CompletedCalled = true;

        protected override void OnCancel(StepCancelReason reason)
        {
            CancelCalled = true;
            LastCancel = reason;
        }
    }

    internal sealed class RecordingLog : ITutorialLog
    {
        public readonly List<string> Lines = new List<string>();
        public readonly List<LogSeverity> Severities = new List<LogSeverity>();

        public void Write(LogSeverity severity, string message)
        {
            Severities.Add(severity);
            Lines.Add(message);
        }
    }

    /// <summary>A trigger the test flips on/off, tracking whether it was re-armed.</summary>
    internal sealed class ManualTrigger : TutorialTriggerBase
    {
        public bool Armed = true;
        public int Resets;

        public override bool ShouldFire(in TriggerContext context) => Armed;

        public override void Reset()
        {
            Resets++;
            Armed = true;
        }
    }

    internal static class Steps
    {
        public static IReadOnlyList<ITutorialStep> List(params ITutorialStep[] steps) => steps;
    }
}
