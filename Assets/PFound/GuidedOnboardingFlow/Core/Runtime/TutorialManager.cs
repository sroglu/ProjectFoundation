using System;
using System.Collections.Generic;

namespace PFound.GuidedOnboardingFlow.Core
{
    /// <summary>
    /// Default <see cref="ITutorialManager"/>. Holds the registered blueprints in registration order (that
    /// order is the tie-break when several are eligible on one tick), enforces a single active run, and
    /// consults an <see cref="ITutorialCompletionStore"/> so finished tutorials never re-trigger.
    /// </summary>
    public sealed class TutorialManager : ITutorialManager
    {
        private readonly List<TutorialBlueprint> _blueprints = new List<TutorialBlueprint>();
        private readonly Dictionary<int, TutorialBlueprint> _byId = new Dictionary<int, TutorialBlueprint>();
        private readonly ITutorialCompletionStore _store;

        private HashSet<int> _runSpecific; // null => no whitelist restriction
        private TutorialRunner _active;
        private TutorialId _activeId;

        private bool _polledOnce;
        private float _timeSinceLaunch;

        public TutorialManager(IEnumerable<TutorialBlueprint> blueprints, ITutorialCompletionStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            if (blueprints != null)
            {
                foreach (TutorialBlueprint bp in blueprints)
                {
                    _blueprints.Add(bp);
                    _byId[bp.Id.handle] = bp;
                }
            }
        }

        public TutorialRunner Active => _active;

        public bool IsRunning => _active != null && _active.Phase == RunnerPhase.Active;

        public bool AutoAdvance { get; set; }

        public event Action<TutorialId> TutorialStarted;
        public event Action<TutorialId, TutorialOutcome> TutorialEnded;
        public event Action<ITutorialStep> StepStarted;

        public bool TryStart(TutorialId id)
        {
            if (IsRunning)
                return false;
            if (!_byId.TryGetValue(id.handle, out TutorialBlueprint bp))
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
                if (_active.Phase == RunnerPhase.Ended)
                    Retire();
                return;
            }

            var context = new TriggerContext(!_polledOnce, _timeSinceLaunch);
            _polledOnce = true;

            TutorialBlueprint chosen = null;
            for (int i = 0; i < _blueprints.Count; i++)
            {
                TutorialBlueprint bp = _blueprints[i];
                if (bp.Mode != TriggerMode.Automatic || bp.Trigger == null)
                    continue;
                if (!IsEligible(bp))
                    continue;
                if (bp.Trigger.ShouldFire(in context))
                {
                    chosen = bp;
                    break; // registration order = deterministic tie-break
                }
            }

            if (chosen != null)
                StartInternal(chosen);
        }

        private bool IsEligible(TutorialBlueprint bp)
        {
            if (_store.IsCompleted(bp.Id))
                return false;
            if (_runSpecific != null && !_runSpecific.Contains(bp.Id.handle))
                return false;
            return true;
        }

        private void StartInternal(TutorialBlueprint bp)
        {
            _active = new TutorialRunner(bp.Instantiate(), bp.Precondition);
            _activeId = bp.Id;
            _active.StepStarted += OnStepStarted;
            _active.Begin();
            TutorialStarted?.Invoke(bp.Id);

            if (_active.Phase == RunnerPhase.Ended)
                Retire();
        }

        private void OnStepStarted(ITutorialStep step) => StepStarted?.Invoke(step);

        private void Retire()
        {
            if (_active == null)
                return;

            TutorialRunner finished = _active;
            TutorialId id = _activeId;
            finished.StepStarted -= OnStepStarted;

            TutorialOutcome outcome = finished.Outcome ?? TutorialOutcome.Shutdown;
            _active = null;

            if (outcome == TutorialOutcome.Completed)
                _store.MarkCompleted(id);

            TutorialEnded?.Invoke(id, outcome);
        }
    }
}
