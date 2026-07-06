using System;
using System.Collections.Generic;
using PFound.GuidedOnboardingFlow.Core;
using UnityEngine;

namespace PFound.GuidedOnboardingFlow.Authoring
{
    /// <summary>
    /// One authored tutorial: identity, label, how it may begin, an optional trigger asset, and its
    /// ordered step assets. <see cref="ToBlueprint"/> turns it into the engine-free blueprint the manager
    /// consumes, binding the concrete services in at that point.
    /// </summary>
    [Serializable]
    public sealed class TutorialDefinition
    {
        [SerializeField] private TutorialId _id;
        [SerializeField] private string _displayName;
        [SerializeField] private TriggerMode _mode = TriggerMode.Manual;
        [SerializeField] private TutorialTriggerAuthoring _trigger;
        [SerializeField] private List<TutorialStepAuthoring> _steps = new List<TutorialStepAuthoring>();

        public TutorialId Id => _id;
        public string DisplayName => _displayName;

        public TutorialBlueprint ToBlueprint(TutorialRuntimeServices services)
        {
            ITutorialTrigger trigger = _mode == TriggerMode.Automatic && _trigger != null
                ? _trigger.CreateTrigger()
                : null;

            return new TutorialBlueprint(
                _id,
                _displayName,
                _mode,
                () => BuildSteps(services),
                trigger);
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

        public IReadOnlyList<TutorialDefinition> Definitions => _definitions;

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
