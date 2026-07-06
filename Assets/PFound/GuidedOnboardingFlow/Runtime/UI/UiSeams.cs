using UnityEngine;

namespace PFound.GuidedOnboardingFlow
{
    /// <summary>
    /// Full-screen raycast catcher. While enabled it swallows input everywhere except an optional
    /// "hole" left open over the current target, and reports when the player interacted through that hole
    /// so an interactive step can complete.
    /// </summary>
    public interface IInputBlocker
    {
        void Enable();
        void Disable();

        /// <summary>Leave a click-through region over <paramref name="target"/>; null re-blocks everything.</summary>
        void SetHole(RectTransform target);

        /// <summary>Returns true exactly once after the player clicked inside the open hole.</summary>
        bool ConsumeInteraction();
    }

    /// <summary>
    /// A pointer/"hand" affordance that animates toward a target to draw the eye. Animation timing is
    /// driven by the TweenPresetLibrary easing curves in the concrete implementation.
    /// </summary>
    public interface ITutorialHand
    {
        void PointAt(RectTransform target);
        void Hide();
    }

    /// <summary>
    /// A dimming overlay with a bright spotlight cut out over the highlighted target, rendered by the
    /// spotlight cutout shader.
    /// </summary>
    public interface IHighlightMask
    {
        void Highlight(RectTransform target);
        void Clear();
    }

    /// <summary>A dialog panel that shows a line of guidance and reports the player's "continue".</summary>
    public interface IDialogPanel
    {
        void Show(string message);
        void Hide();

        /// <summary>Returns true exactly once after the player advanced the dialog.</summary>
        bool ConsumeContinue();
    }

    /// <summary>Owns the tutorial overlay canvas and its show/hide + sorting.</summary>
    public interface ITutorialCanvas
    {
        void SetVisible(bool visible);
        Canvas Canvas { get; }
    }

    /// <summary>
    /// Maps a string tag to a live scene/UI transform so steps can target elements by tag without holding
    /// scene references. Anchors register themselves on enable and unregister on disable.
    /// </summary>
    public interface ITutorialAnchorRegistry
    {
        void Register(string tag, RectTransform anchor);
        void Unregister(string tag);
        bool TryResolve(string tag, out RectTransform anchor);
    }
}
