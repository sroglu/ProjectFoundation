using System;
using PFound.GuidedOnboardingFlow.Core;
using RouterService = PFound.ScreenRouter.ScreenRouter;
using ScreenBase = PFound.ScreenRouter.Screen;

namespace PFound.GuidedOnboardingFlow
{
    /// <summary>
    /// Opens a screen through the PFound ScreenRouter and finishes once it is confirmed open. Because the
    /// router's open/query API is generic over the screen type, the step is handed open/verify delegates
    /// rather than a runtime <see cref="Type"/>; use <see cref="For{TScreen}"/> for the common case. Its
    /// readiness check is <see cref="StepReadiness.Unreachable"/> when there is no router to drive
    /// (a hard authoring/wiring error) and <see cref="StepReadiness.Ready"/> otherwise. If the target
    /// screen never lands within <see cref="_landingTimeoutSeconds"/>, the step aborts the run rather than
    /// waiting forever.
    /// </summary>
    public sealed class OpenScreenStep : TutorialStepBase
    {
        private readonly RouterService _router;
        private readonly Action<RouterService> _open;
        private readonly Func<RouterService, bool> _isOpen;
        private readonly float _landingTimeoutSeconds;

        private bool _opened;

        public OpenScreenStep(
            RouterService router,
            Action<RouterService> open,
            Func<RouterService, bool> isOpen = null,
            float landingTimeoutSeconds = 5f,
            float? timeout = null)
        {
            _router = router;
            _open = open;
            _isOpen = isOpen;
            _landingTimeoutSeconds = landingTimeoutSeconds;
            Timeout = timeout;
        }

        /// <summary>Build a step that opens and waits on a specific screen type.</summary>
        public static OpenScreenStep For<TScreen>(RouterService router, float landingTimeoutSeconds = 5f, float? timeout = null)
            where TScreen : ScreenBase
        {
            return new OpenScreenStep(
                router,
                r => r.SwitchScreen<TScreen>(),
                r => r.IsScreenOpen<TScreen>(),
                landingTimeoutSeconds,
                timeout);
        }

        protected override StepReadiness OnCheckReadiness()
        {
            // No router wired → this step can never open its screen; abort rather than silently hang.
            return _router == null ? StepReadiness.Unreachable : StepReadiness.Ready;
        }

        protected override void OnBegin()
        {
            _opened = false;
        }

        protected override StepStatus OnAdvance(float deltaSeconds)
        {
            if (!_opened)
            {
                _open?.Invoke(_router);
                _opened = true;
            }

            if (_isOpen == null || _isOpen(_router))
                return StepStatus.Finished;

            // The generic per-step Timeout (from TutorialStepBase) handles the "give up" budget cleanly;
            // _landingTimeoutSeconds is the specific "screen never landed" abort when no Timeout was set.
            if (!Timeout.HasValue && _landingTimeoutSeconds > 0f && Elapsed >= _landingTimeoutSeconds)
                throw new TimeoutException(
                    "OpenScreenStep: target screen never became active within " +
                    _landingTimeoutSeconds.ToString("0.0") + "s.");

            return StepStatus.Running;
        }
    }
}
