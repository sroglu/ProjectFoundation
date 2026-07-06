using System.Collections.Generic;
using UnityEngine;

namespace PFound.GuidedOnboardingFlow
{
    /// <summary>
    /// Plain dictionary-backed anchor registry. A single instance is shared via DI; scene anchors push
    /// themselves in and out of it as they enable/disable.
    /// </summary>
    public sealed class TutorialAnchorRegistry : ITutorialAnchorRegistry
    {
        private readonly Dictionary<string, RectTransform> _anchors = new Dictionary<string, RectTransform>();

        public void Register(string tag, RectTransform anchor)
        {
            if (string.IsNullOrEmpty(tag) || anchor == null)
                return;
            _anchors[tag] = anchor;
        }

        public void Unregister(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return;
            _anchors.Remove(tag);
        }

        public bool TryResolve(string tag, out RectTransform anchor)
        {
            if (!string.IsNullOrEmpty(tag) && _anchors.TryGetValue(tag, out anchor) && anchor != null)
                return true;
            anchor = null;
            return false;
        }
    }

    /// <summary>
    /// Drop this on any UI element that a tutorial step should be able to target by tag. It registers on
    /// enable and unregisters on disable so the registry only ever holds live transforms.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneAnchor : MonoBehaviour
    {
        [SerializeField] private string _tag;

        private ITutorialAnchorRegistry _registry;

        /// <summary>Injected by the bootstrap before the anchor enables; safe to set in Awake wiring.</summary>
        public void Bind(ITutorialAnchorRegistry registry)
        {
            _registry = registry;
            if (isActiveAndEnabled)
                Push();
        }

        private void OnEnable() => Push();

        private void OnDisable() => _registry?.Unregister(_tag);

        private void Push()
        {
            _registry?.Register(_tag, transform as RectTransform);
        }
    }
}
