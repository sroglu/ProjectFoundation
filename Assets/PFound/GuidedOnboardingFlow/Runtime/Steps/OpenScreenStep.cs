using System;
using PFound.GuidedOnboardingFlow.Core;
using RouterService = PFound.ScreenRouter.ScreenRouter;
using ScreenBase = PFound.ScreenRouter.ContentBase;

namespace PFound.GuidedOnboardingFlow
{
    /// <summary>
    /// Opens a screen through the PFound ScreenRouter and finishes once it is confirmed open. Because the
    /// router's open/query API is generic over the screen type, the step is handed open/verify delegates
    /// rather than a runtime <see cref="Type"/>; use <see cref="For{TScreen}"/> for the common case.
    /// </summary>
    public sealed class OpenScreenStep : TutorialStepBase
    {
        private readonly RouterService _router;
        private readonly Action<RouterService> _open;
        private readonly Func<RouterService, bool> _isOpen;

        public OpenScreenStep(
            RouterService router,
            Action<RouterService> open,
            Func<RouterService, bool> isOpen = null,
            float? timeout = null)
        {
            _router = router;
            _open = open;
            _isOpen = isOpen;
            Timeout = timeout;
        }

        /// <summary>Build a step that opens and waits on a specific screen type.</summary>
        public static OpenScreenStep For<TScreen>(RouterService router, float? timeout = null)
            where TScreen : ScreenBase
        {
            return new OpenScreenStep(
                router,
                r => r.OpenFrame<TScreen>(),
                r => r.IsScreenOpen<TScreen>(),
                timeout);
        }

        protected override void OnBegin()
        {
            _open?.Invoke(_router);
        }

        protected override StepStatus OnAdvance(float deltaSeconds)
        {
            if (_isOpen == null || _isOpen(_router))
                return StepStatus.Finished;
            return StepStatus.Running;
        }
    }
}
