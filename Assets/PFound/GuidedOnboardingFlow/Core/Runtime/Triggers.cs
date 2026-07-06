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

        public TriggerContext(bool isFirstPoll, float timeSinceLaunch)
        {
            IsFirstPoll = isFirstPoll;
            TimeSinceLaunch = timeSinceLaunch;
        }
    }

    /// <summary>
    /// Decides whether an automatic tutorial wants to run right now. The manager only polls triggers of
    /// tutorials that are otherwise eligible (not completed, whitelisted if a whitelist is set).
    /// </summary>
    public interface ITutorialTrigger
    {
        bool ShouldFire(in TriggerContext context);
    }

    /// <summary>Convenience base so subclasses implement a single method.</summary>
    public abstract class TutorialTriggerBase : ITutorialTrigger
    {
        public abstract bool ShouldFire(in TriggerContext context);
    }

    /// <summary>
    /// Fires exactly once, on the first poll after launch. The canonical "show this the moment the app
    /// is ready" trigger; latches so it never re-fires within a session.
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
    }
}
