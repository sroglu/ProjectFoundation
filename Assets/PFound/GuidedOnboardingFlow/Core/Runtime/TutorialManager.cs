using System;
using System.Collections.Generic;

namespace PFound.GuidedOnboardingFlow.Core
{
    /// <summary>
    /// Default <see cref="ITutorialManager"/>. Holds the registered blueprints in registration order (that
    /// order is the tie-break when several are eligible on one tick), enforces a single active run, and
    /// consults an <see cref="ITutorialCompletionStore"/> so finished tutorials never re-trigger (unless
    /// their <see cref="ReplayPolicy"/> is <see cref="ReplayPolicy.Repeatable"/>). Each blueprint's
    /// triggers combine with AND; when a run ends, its triggers are re-armed so a repeatable/once-session
    /// tutorial can fire again. Optional lifecycle diagnostics flow through an <see cref="ITutorialLog"/>
    /// gated by <see cref="LogVerbosity"/>.
    /// </summary>
    public sealed class TutorialManager : ITutorialManager
    {
        private readonly List<TutorialBlueprint> _blueprints = new List<TutorialBlueprint>();
        private readonly Dictionary<int, TutorialBlueprint> _byId = new Dictionary<int, TutorialBlueprint>();
        private readonly Dictionary<int, int> _runCounts = new Dictionary<int, int>();
        private readonly ITutorialCompletionStore _store;
        private readonly ITutorialLog _log;
        private readonly LogVerbosity _verbosity;

        private HashSet<int> _runSpecific; // null => no whitelist restriction
        private TutorialRunner _active;
        private TutorialBlueprint _activeBlueprint;
        private TutorialId _activeId;
        private int _lastEmittedStepIndex = -1;

        private bool _polledOnce;
        private float _timeSinceLaunch;

        public TutorialManager(
            IEnumerable<TutorialBlueprint> blueprints,
            ITutorialCompletionStore store,
            ITutorialLog log = null,
            LogVerbosity verbosity = LogVerbosity.Errors)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _log = log;
            _verbosity = verbosity;

            if (blueprints != null)
            {
                var seen = new HashSet<int>();
                foreach (TutorialBlueprint bp in blueprints)
                {
                    if (bp == null)
                        continue;

                    // Startup validation (dup / empty id): the first registration under a handle wins so
                    // later duplicates are unreachable; an unset id (handle 0) can never be started by id.
                    if (bp.Id.IsNone)
                        Warn("Tutorial '" + bp.DisplayName + "' has no id (handle 0) — it can never be started by id.");
                    else if (!seen.Add(bp.Id.handle))
                        Warn("Duplicate tutorial id " + bp.Id + " — only the first registration is reachable.");

                    _blueprints.Add(bp);
                    if (!bp.Id.IsNone && !_byId.ContainsKey(bp.Id.handle))
                        _byId[bp.Id.handle] = bp;
                }
            }
        }

        public TutorialRunner Active => _active;

        public bool IsRunning => _active != null && _active.Phase == RunnerPhase.Active;

        // Auto-advance is honored by the interactive steps themselves (they read these off the manager via
        // TutorialRuntimeServices): a step self-completes once its elapsed time passes the per-step delay,
        // and a focus step additionally synthesizes a real pointer event so the game's own handler fires.
        public bool AutoAdvance { get; set; }

        public float AutoAdvanceDelaySeconds { get; private set; }

        public event Action<TutorialId> TutorialStarted;
        public event Action<TutorialId, TutorialOutcome> TutorialEnded;
        public event Action<ITutorialStep> StepStarted;
        public event Action<TutorialId, int> StepChanged;

        public void SetAutoAdvance(bool enabled, float perStepDelaySeconds)
        {
            AutoAdvance = enabled;
            AutoAdvanceDelaySeconds = perStepDelaySeconds < 0f ? 0f : perStepDelaySeconds;
        }

        public bool TryStart(TutorialId id)
        {
            if (IsRunning)
                return false;
            if (!_byId.TryGetValue(id.handle, out TutorialBlueprint bp))
                return false;
            // Respect completion — a polite start of an already-finished (non-repeatable) tutorial is a no-op.
            if (bp.Replay != ReplayPolicy.Repeatable && _store.IsCompleted(bp.Id))
                return false;
            StartInternal(bp);
            return true;
        }

        public void ForceStart(TutorialId id)
        {
            if (!_byId.TryGetValue(id.handle, out TutorialBlueprint bp))
                return;
            if (_active != null && _active.Phase == RunnerPhase.Active)
            {
                _active.NotifyShutdown();
                Retire();
            }
            StartInternal(bp);
        }

        public void Skip()
        {
            if (_active == null || _active.Phase != RunnerPhase.Active)
                return;
            _active.Skip();
            Retire();
        }

        public void SetRunSpecific(IEnumerable<TutorialId> ids)
        {
            if (ids == null)
            {
                _runSpecific = null;
                return;
            }

            var set = new HashSet<int>();
            foreach (TutorialId id in ids)
                set.Add(id.handle);
            _runSpecific = set.Count == 0 ? null : set;
        }

        public void Tick(float deltaSeconds)
        {
            _timeSinceLaunch += deltaSeconds;

            if (_active != null)
            {
                _active.Tick(deltaSeconds);
                EmitStepChangedIfMoved();
                if (_active.Phase == RunnerPhase.Ended)
                    Retire();
                return;
            }

            bool isFirstPoll = !_polledOnce;
            _polledOnce = true;

            TutorialBlueprint chosen = null;
            for (int i = 0; i < _blueprints.Count; i++)
            {
                TutorialBlueprint bp = _blueprints[i];
                if (bp.Mode != TriggerMode.Automatic || bp.Triggers.Count == 0)
                    continue;
                if (!IsEligible(bp))
                    continue;

                var context = new TriggerContext(isFirstPoll, _timeSinceLaunch, bp.Id, RunCountOf(bp.Id));
                if (AllTriggersFire(bp, in context))
                {
                    chosen = bp;
                    break; // registration order = deterministic tie-break
                }
            }

            if (chosen != null)
                StartInternal(chosen);
        }

        private static bool AllTriggersFire(TutorialBlueprint bp, in TriggerContext context)
        {
            // AND semantics: every trigger must fire on this same poll.
            for (int i = 0; i < bp.Triggers.Count; i++)
            {
                ITutorialTrigger trigger = bp.Triggers[i];
                if (trigger == null || !trigger.ShouldFire(in context))
                    return false;
            }
            return true;
        }

        private bool IsEligible(TutorialBlueprint bp)
        {
            if (bp.Replay != ReplayPolicy.Repeatable && _store.IsCompleted(bp.Id))
                return false;
            if (_runSpecific != null && !_runSpecific.Contains(bp.Id.handle))
                return false;
            return true;
        }

        private int RunCountOf(TutorialId id) => _runCounts.TryGetValue(id.handle, out int n) ? n : 0;

        private void StartInternal(TutorialBlueprint bp)
        {
            _active = new TutorialRunner(bp.Instantiate(), bp.Precondition);
            _activeBlueprint = bp;
            _activeId = bp.Id;
            _lastEmittedStepIndex = -1;
            _active.StepStarted += OnStepStarted;

            Lifecycle("start " + bp.Id + " '" + bp.DisplayName + "'");
            _active.Begin();
            TutorialStarted?.Invoke(bp.Id);
            EmitStepChangedIfMoved();

            if (_active.Phase == RunnerPhase.Ended)
                Retire();
        }

        private void OnStepStarted(ITutorialStep step) => StepStarted?.Invoke(step);

        private void EmitStepChangedIfMoved()
        {
            if (_active == null || _active.Phase != RunnerPhase.Active)
                return;
            if (_active.StepIndex == _lastEmittedStepIndex)
                return;
            _lastEmittedStepIndex = _active.StepIndex;
            Lifecycle("step " + _activeId + " -> " + _lastEmittedStepIndex);
            StepChanged?.Invoke(_activeId, _lastEmittedStepIndex);
        }

        private void Retire()
        {
            if (_active == null)
                return;

            TutorialRunner finished = _active;
            TutorialBlueprint bp = _activeBlueprint;
            TutorialId id = _activeId;
            finished.StepStarted -= OnStepStarted;

            TutorialOutcome outcome = finished.Outcome ?? TutorialOutcome.Shutdown;
            _active = null;
            _activeBlueprint = null;

            _runCounts[id.handle] = RunCountOf(id) + 1;

            // Persist on a normal finish or a player skip — both mean "the player has seen this". A skip
            // records under the tutorial's replay policy just like a completion so it does not re-nag.
            if (outcome == TutorialOutcome.Completed || outcome == TutorialOutcome.Skipped)
                _store.MarkCompleted(id, bp != null ? bp.Replay : ReplayPolicy.OnceAccount);

            // Re-arm triggers so once-session / repeatable tutorials can fire again this session.
            bp?.ResetTriggers();

            Lifecycle("end " + id + " -> " + outcome);
            TutorialEnded?.Invoke(id, outcome);
        }

        private void Lifecycle(string message)
        {
            if (_log != null && _verbosity != LogVerbosity.Errors)
                _log.Write(LogSeverity.Info, message);
        }

        private void Warn(string message) => _log?.Write(LogSeverity.Warning, message);
    }
}
