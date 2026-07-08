using System.Collections.Generic;
using UnityEngine;

namespace PFound.GuidedOnboardingFlow
{
    /// <summary>
    /// Full-screen raycast catcher. While enabled it swallows input everywhere except an optional
    /// "hole" left open over the current target, and reports when the player interacted through that hole
    /// so an interactive step can complete. The concrete blocker also re-executes the pointer
    /// press/release/click on the actual target (and its children) so the game's own handler still fires —
    /// the player really presses the button, not just "the tutorial".
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
    /// A pointer/"hand" affordance that animates toward a target to draw the eye, optionally showing a
    /// short hint caption. Animation timing is driven by the TweenPresetLibrary easing curves in the
    /// concrete implementation.
    /// </summary>
    public interface ITutorialHand
    {
        void PointAt(RectTransform target);

        /// <summary>Point at a target with a hint caption and a per-anchor offset applied to the pointer.</summary>
        void PointAt(RectTransform target, string hint, Vector2 offset);

        void Hide();
    }

    /// <summary>
    /// A dimming overlay with a bright spotlight cut out over the highlighted target, rendered by the
    /// spotlight cutout shader. The cutout can be a plain rectangle or shaped by a named mask sprite, and
    /// grown by a per-anchor padding so a small icon still gets breathing room.
    /// </summary>
    public interface IHighlightMask
    {
        void Highlight(RectTransform target);

        /// <summary>Highlight with an optional sprite-shaped cutout and extra padding around the target.</summary>
        void Highlight(RectTransform target, string maskSpriteName, Vector2 padding);

        void Clear();
    }

    /// <summary>How a dialog line is dismissed.</summary>
    public enum DialogDismissMode
    {
        /// <summary>Only a tap advances it.</summary>
        Tap,

        /// <summary>Only the auto-dismiss timer advances it (no tap).</summary>
        Auto,

        /// <summary>Either a tap or the timer advances it.</summary>
        Both
    }

    /// <summary>A dialog panel that shows a line of guidance and reports the player's "continue".</summary>
    public interface IDialogPanel
    {
        void Show(string message);

        /// <summary>
        /// Show <paramref name="message"/> with a typewriter reveal at <paramref name="typingSpeedCps"/>
        /// characters/second (≤0 = instant), dismissed per <paramref name="dismissMode"/>. When the mode
        /// involves auto-dismiss, <paramref name="autoDismissSeconds"/> is how long the fully-revealed
        /// line stays before it advances itself.
        /// </summary>
        void Show(string message, float typingSpeedCps, DialogDismissMode dismissMode, float? autoDismissSeconds);

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
    /// A resolved anchor: the live UI transform plus the per-anchor presentation hints a focus step
    /// applies — a pointer <see cref="Offset"/>, a highlight <see cref="Padding"/>, and an optional
    /// <see cref="MaskSpriteName"/> that shapes the spotlight cutout.
    /// </summary>
    public readonly struct TutorialAnchorHandle
    {
        public readonly RectTransform Rect;
        public readonly Vector2 Offset;
        public readonly Vector2 Padding;
        public readonly string MaskSpriteName;

        public TutorialAnchorHandle(RectTransform rect, Vector2 offset = default, Vector2 padding = default, string maskSpriteName = null)
        {
            Rect = rect;
            Offset = offset;
            Padding = padding;
            MaskSpriteName = maskSpriteName;
        }
    }

    /// <summary>
    /// Maps a string tag to one or more live scene/UI transforms so steps can target elements by tag
    /// without holding scene references. Anchors register themselves on enable and unregister on disable.
    /// Several anchors may share a tag (e.g. a repeated list row); single-target steps take the first
    /// registered, and <see cref="ResolveAll"/> exposes the whole set.
    /// </summary>
    public interface ITutorialAnchorRegistry
    {
        void Register(string tag, RectTransform anchor);

        /// <summary>Register a fully-specified anchor (with offset/padding/mask hints).</summary>
        void Register(string tag, TutorialAnchorHandle handle);

        /// <summary>Remove every anchor registered under the tag.</summary>
        void Unregister(string tag);

        /// <summary>Remove one specific anchor from the tag (leaving any siblings).</summary>
        void Unregister(string tag, RectTransform anchor);

        bool TryResolve(string tag, out RectTransform anchor);

        /// <summary>Resolve the first anchor under the tag with its presentation hints.</summary>
        bool TryResolveHandle(string tag, out TutorialAnchorHandle handle);

        /// <summary>Every anchor currently registered under the tag (empty if none).</summary>
        IReadOnlyList<TutorialAnchorHandle> ResolveAll(string tag);

        /// <summary>Total number of active anchor registrations across every tag.</summary>
        int ActiveCount { get; }
    }
}
