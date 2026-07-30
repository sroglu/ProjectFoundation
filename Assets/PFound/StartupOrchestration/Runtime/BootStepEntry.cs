using System;
using UnityEngine;

namespace PFound.StartupOrchestration
{
    /// <summary>
    /// One row in a <see cref="BootStepManifest"/>: a concrete <see cref="IStartupStep"/> a designer
    /// picked, plus an on/off toggle so a step can stay in the list but be skipped for a build.
    ///
    /// The step is held with <see cref="SerializeReference"/>, so the inspector shows a type picker and
    /// the chosen step's own fields inline. A step type must be <c>[Serializable]</c> and have a
    /// no-argument constructor for the inspector to create it; steps that need constructor inputs are
    /// wired in code instead of through the manifest.
    /// </summary>
    [Serializable]
    public sealed class BootStepEntry
    {
        [SerializeField]
        [Tooltip("Turn off to keep this step in the list but skip it at boot.")]
        private bool _enabled = true;

        [SerializeReference]
        [Tooltip("The concrete boot step to run. Pick a type, then set its fields inline.")]
        private IStartupStep _step;

        /// <summary>Whether this step should run.</summary>
        public bool Enabled => _enabled;

        /// <summary>
        /// The concrete step, or <c>null</c> if the designer left the row empty. That empty slot is
        /// author-supplied data, not our own state, so callers skip it rather than treat null as a bug.
        /// </summary>
        public IStartupStep Step => _step;

        public BootStepEntry() { }

        public BootStepEntry(IStartupStep step, bool enabled = true)
        {
            _step = step;
            _enabled = enabled;
        }
    }
}
