using System.Collections.Generic;
using UnityEngine;

namespace PFound.GuidedOnboardingFlow
{
    /// <summary>
    /// Plain dictionary-backed anchor registry. A single instance is shared via DI; scene anchors push
    /// themselves in and out of it as they enable/disable. Multiple anchors may share a tag — they are
    /// kept in registration order, single-target steps take the first, and stale (destroyed) entries are
    /// swept lazily on resolve.
    /// </summary>
    public sealed class TutorialAnchorRegistry : ITutorialAnchorRegistry
    {
        private static readonly IReadOnlyList<TutorialAnchorHandle> Empty = new TutorialAnchorHandle[0];

        private readonly Dictionary<string, List<TutorialAnchorHandle>> _byTag =
            new Dictionary<string, List<TutorialAnchorHandle>>();

        public int ActiveCount
        {
            get
            {
                int total = 0;
                foreach (List<TutorialAnchorHandle> list in _byTag.Values)
                    total += list.Count;
                return total;
            }
        }

        public void Register(string tag, RectTransform anchor)
        {
            Register(tag, new TutorialAnchorHandle(anchor));
        }

        public void Register(string tag, TutorialAnchorHandle handle)
        {
            if (string.IsNullOrEmpty(tag) || handle.Rect == null)
                return;

            if (!_byTag.TryGetValue(tag, out List<TutorialAnchorHandle> list))
            {
                list = new List<TutorialAnchorHandle>(2);
                _byTag[tag] = list;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Rect == handle.Rect)
                {
                    list[i] = handle; // refresh hints for an already-registered transform
                    return;
                }
            }
            list.Add(handle);
        }

        public void Unregister(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return;
            _byTag.Remove(tag);
        }

        public void Unregister(string tag, RectTransform anchor)
        {
            if (string.IsNullOrEmpty(tag) || anchor == null)
                return;
            if (!_byTag.TryGetValue(tag, out List<TutorialAnchorHandle> list))
                return;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].Rect == anchor)
                    list.RemoveAt(i);
            }
            if (list.Count == 0)
                _byTag.Remove(tag);
        }

        public bool TryResolve(string tag, out RectTransform anchor)
        {
            if (TryResolveHandle(tag, out TutorialAnchorHandle handle))
            {
                anchor = handle.Rect;
                return true;
            }
            anchor = null;
            return false;
        }

        public bool TryResolveHandle(string tag, out TutorialAnchorHandle handle)
        {
            handle = default;
            if (string.IsNullOrEmpty(tag) || !_byTag.TryGetValue(tag, out List<TutorialAnchorHandle> list))
                return false;

            Sweep(list);
            if (list.Count == 0)
            {
                _byTag.Remove(tag);
                return false;
            }
            handle = list[0];
            return true;
        }

        public IReadOnlyList<TutorialAnchorHandle> ResolveAll(string tag)
        {
            if (string.IsNullOrEmpty(tag) || !_byTag.TryGetValue(tag, out List<TutorialAnchorHandle> list))
                return Empty;
            Sweep(list);
            return list;
        }

        // Drop entries whose transform has been destroyed (Unity fake-null aware).
        private static void Sweep(List<TutorialAnchorHandle> list)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].Rect == null)
                    list.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Drop this on any UI element that a tutorial step should be able to target by tag. It registers on
    /// enable and unregisters on disable so the registry only ever holds live transforms. The optional
    /// <see cref="_offset"/> nudges only the hand pointer, <see cref="_padding"/> grows the spotlight
    /// cutout + click region symmetrically, and <see cref="_maskSpriteName"/> shapes the cutout.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneAnchor : MonoBehaviour
    {
        [SerializeField] private string _tag;
        [Tooltip("Nudges ONLY the hand pointer (e.g. a 'look up' arrow), never the click region.")]
        [SerializeField] private Vector2 _offset;
        [Tooltip("Grows the spotlight + click region symmetrically around the target.")]
        [SerializeField] private Vector2 _padding;
        [Tooltip("Optional named mask sprite that shapes the spotlight cutout.")]
        [SerializeField] private string _maskSpriteName;

        private ITutorialAnchorRegistry _registry;

        /// <summary>Injected by the bootstrap before the anchor enables; safe to set in Awake wiring.</summary>
        public void Bind(ITutorialAnchorRegistry registry)
        {
            _registry = registry;
            if (isActiveAndEnabled)
                Push();
        }

        private void OnEnable() => Push();

        private void OnDisable() => _registry?.Unregister(_tag, transform as RectTransform);

        private void Push()
        {
            var rect = transform as RectTransform;
            if (rect == null)
                return;
            _registry?.Register(_tag, new TutorialAnchorHandle(rect, _offset, _padding, _maskSpriteName));
        }
    }
}
