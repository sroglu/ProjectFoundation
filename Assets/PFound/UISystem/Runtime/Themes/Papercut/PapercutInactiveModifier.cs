using UnityEngine;
using UnityEngine.UIElements;

namespace PFound.UISystem.Themes.Papercut
{
    /// <summary>
    /// Toggles the <c>pc-inactive</c> USS class on a Papercut surface whose
    /// underlying control reports a "currently-off-but-interactive" state
    /// (Chip unselected, Switch off, Segmented unselected).
    ///
    /// UI Toolkit does not expose an <c>:inactive</c> pseudo, so the data-model's
    /// fourth Component State is plumbed via this class modifier instead.
    /// </summary>
    public static class PapercutInactiveModifier
    {
        private const string InactiveClass = "pc-inactive";

        /// <summary>
        /// Wires <paramref name="toggle"/> so the surface element gains the
        /// <c>pc-inactive</c> class whenever <c>value</c> is false.
        /// The surface is found by walking up from the Toggle until a
        /// <c>.pc-surface</c> ancestor is hit (so the same call works
        /// regardless of where in the template the Toggle sits).
        /// </summary>
        public static void AttachTo(Toggle toggle)
        {
            var surface = FindSurface(toggle);
            ApplyInactive(surface, !toggle.value);
            toggle.RegisterValueChangedCallback(evt => ApplyInactive(surface, !evt.newValue));
        }

        /// <summary>
        /// Generic surface modifier — call from component-specific code that
        /// already knows whether the control is currently inactive.
        /// </summary>
        public static void SetInactive(VisualElement surface, bool isInactive)
        {
            ApplyInactive(surface, isInactive);
        }

        private static VisualElement FindSurface(VisualElement node)
        {
            var current = node;
            while (current != null)
            {
                if (current.ClassListContains("pc-surface")) return current;
                current = current.parent;
            }
            Debug.LogWarning($"[Papercut] No .pc-surface ancestor found for {node?.name ?? "<null>"} — Inactive class will be applied to the element itself.");
            return node;
        }

        private static void ApplyInactive(VisualElement surface, bool isInactive)
        {
            if (surface == null) return;
            if (isInactive) surface.AddToClassList(InactiveClass);
            else surface.RemoveFromClassList(InactiveClass);
        }
    }
}
