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

        private bool _finishNow;
        private StepReadiness _readiness = StepReadiness.Ready;
        private readonly bool _throwOnBegin;
        private readonly bool _throwOnAdvance;

        public ProbeStep(float? timeout = null, bool throwOnBegin = false, bool throwOnAdvance = false)
        {
            Timeout = timeout;
            _throwOnBegin = throwOnBegin;
            _throwOnAdvance = throwOnAdvance;
        }

        public void FinishNext() => _finishNow = true;

        public void SetReadiness(StepReadiness readiness) => _readiness = readiness;

        protected override StepReadiness OnCheckReadiness() => _readiness;

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

        public void Write(string message) => Lines.Add(message);
    }

    internal static class Steps
    {
        public static IReadOnlyList<ITutorialStep> List(params ITutorialStep[] steps) => steps;
    }
}
