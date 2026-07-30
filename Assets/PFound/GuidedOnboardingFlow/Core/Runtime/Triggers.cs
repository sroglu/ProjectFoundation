namespace PFound.GuidedOnboardingFlow.Core
{
    /// <summary>
    /// Read-only snapshot handed to a trigger each time the manager polls it, so triggers stay pure
    /// functions of ambient state rather than reaching for globals.
    /// </summary>
    public readonly struct TriggerContext
    {
        /// <summary>True on the manager's very first poll after launch.</summary>
        public readonly bool IsFirstPoll;

        /// <summary>Seconds since the manager started polling.</summary>
        public readonly float TimeSinceLaunch;

        /// <summary>Identity of the tutorial whose triggers are currently being evaluated.</summary>
        public readonly TutorialId TutorialId;

        /// <summary>How many times this tutorial has already completed a run this session.</summary>
        public readonly int RunCount;

        public TriggerContext(bool isFirstPoll, float timeSinceLaunch, TutorialId tutorialId = default, int runCount = 0)
        {
            IsFirstPoll = isFirstPoll;
            TimeSinceLaunch = timeSinceLaunch;
            TutorialId = tutorialId;
            RunCount = runCount;
        }
    }

    /// <summary>
    /// Decides whether an automatic tutorial wants to run right now. The manager only polls triggers of
    /// tutorials that are otherwise eligible (not completed, whitelisted if a whitelist is set). A tutorial
    /// may carry several triggers; they combine with AND — every one must return true on the same poll.
    /// </summary>
    public interface ITutorialTrigger
    {
        bool ShouldFire(in TriggerContext context);

        /// <summary>
        /// Re-arm the trigger's local state (e.g. a "fired once" latch) so a repeatable / once-session
        /// tutorial can trigger again after a run ends. Called by the manager when the run it belongs to
        /// finishes. Stateless triggers leave this a no-op.
        /// </summary>
        void Reset();
    }

    /// <summary>Convenience base so subclasses implement a single method; <see cref="Reset"/> defaults to a no-op.</summary>
    public abstract class TutorialTriggerBase : ITutorialTrigger
    {
        public abstract bool ShouldFire(in TriggerContext context);

        public virtual void Reset()
        {
        }
    }

    /// <summary>
    /// Fires exactly once, on the first poll after launch. The canonical "show this the moment the app
    /// is ready" trigger; latches so it never re-fires until <see cref="Reset"/> re-arms it (which the
    /// manager does when the tutorial's run ends, letting a once-session/repeatable tutorial fire again).
    /// </summary>
    public sealed class StartupTrigger : TutorialTriggerBase
    {
        private bool _spent;

        public override bool ShouldFire(in TriggerContext context)
        {
            if (_spent)
                return false;
            if (!context.IsFirstPoll)
                return false;
            _spent = true;
            return true;
        }

        public override void Reset() => _spent = false;
    }

    /// <summary>
    /// Fires once the manager has been polling for at least <see cref="_afterSeconds"/> — a simple
    /// "let the player settle in before nagging" trigger. Latches like <see cref="StartupTrigger"/>.
    /// </summary>
    public sealed class DelayedStartupTrigger : TutorialTriggerBase
    {
        private readonly float _afterSeconds;
        private bool _spent;

        public DelayedStartupTrigger(float afterSeconds)
        {
            _afterSeconds = afterSeconds;
        }

        public override bool ShouldFire(in TriggerContext context)
        {
            if (_spent)
                return false;
            if (context.TimeSinceLaunch < _afterSeconds)
                return false;
            _spent = true;
            return true;
        }

        public override void Reset() => _spent = false;
    }
}
