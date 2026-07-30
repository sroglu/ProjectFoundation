using System;
using System.Collections.Generic;
using PFound.GuidedOnboardingFlow.Core;
using UnityEngine;

namespace PFound.GuidedOnboardingFlow.Authoring
{
    /// <summary>
    /// One authored tutorial: identity, label, how it may begin, its (AND-combined) trigger assets, its
    /// replay policy, a per-tutorial default timeout outcome, and its ordered step assets.
    /// <see cref="ToBlueprint"/> turns it into the engine-free blueprint the manager consumes, binding the
    /// concrete services in at that point.
    /// </summary>
    [Serializable]
    public sealed class TutorialDefinition
    {
        [SerializeField] private TutorialId _id;
        [SerializeField] private string _displayName;
        [SerializeField] private TriggerMode _mode = TriggerMode.Manual;

        [Tooltip("All triggers must fire on the same poll (AND) for an automatic start.")]
        [SerializeField] private List<TutorialTriggerAuthoring> _triggers = new List<TutorialTriggerAuthoring>();

        [Tooltip("How a completed run is remembered so it does not re-fire.")]
        [SerializeField] private ReplayPolicy _replay = ReplayPolicy.OnceAccount;

        [Tooltip("What a step timeout does when the step itself doesn't specify.")]
        [SerializeField] private StepTimeoutOutcome _defaultTimeoutOutcome = StepTimeoutOutcome.Advance;

        [SerializeField] private List<TutorialStepAuthoring> _steps = new List<TutorialStepAuthoring>();

        public TutorialId Id => _id;
        public string DisplayName => _displayName;

        public TutorialBlueprint ToBlueprint(TutorialRuntimeServices services)
        {
            var triggers = new List<ITutorialTrigger>();
            if (_mode == TriggerMode.Automatic)
            {
                for (int i = 0; i < _triggers.Count; i++)
                {
                    if (_triggers[i] != null)
                        triggers.Add(_triggers[i].CreateTrigger());
                }
            }

            return new TutorialBlueprint(
                _id,
                _displayName,
                _mode,
                () => BuildSteps(services),
                triggers,
                precondition: null,
                replay: _replay,
                defaultTimeoutOutcome: _defaultTimeoutOutcome);
        }

        private IReadOnlyList<ITutorialStep> BuildSteps(TutorialRuntimeServices services)
        {
            var list = new List<ITutorialStep>(_steps.Count);
            for (int i = 0; i < _steps.Count; i++)
            {
                if (_steps[i] != null)
                    list.Add(_steps[i].CreateStep(services));
            }
            return list;
        }
    }

    /// <summary>The config asset: the full set of authored tutorial definitions for a project.</summary>
    [CreateAssetMenu(menuName = "PFound/Guided Onboarding/Catalog", fileName = "TutorialCatalog")]
    public sealed class TutorialCatalog : ScriptableObject
    {
        [SerializeField] private List<TutorialDefinition> _definitions = new List<TutorialDefinition>();

        [Tooltip("When non-empty, only these tutorials are eligible to auto-run (applied via SetRunSpecific).")]
        [SerializeField] private List<TutorialId> _runWhitelist = new List<TutorialId>();

        [Tooltip("How chatty the manager's lifecycle logging is.")]
        [SerializeField] private LogVerbosity _logVerbosity = LogVerbosity.Errors;

        public IReadOnlyList<TutorialDefinition> Definitions => _definitions;
        public IReadOnlyList<TutorialId> RunWhitelist => _runWhitelist;
        public LogVerbosity LogVerbosity => _logVerbosity;

        /// <summary>Resolve every definition into a blueprint, preserving authoring order (the tie-break).</summary>
        public List<TutorialBlueprint> BuildBlueprints(TutorialRuntimeServices services)
        {
            var result = new List<TutorialBlueprint>(_definitions.Count);
            foreach (TutorialDefinition def in _definitions)
                result.Add(def.ToBlueprint(services));
            return result;
        }
    }
}
