namespace PFound.TweenPresetLibrary.Core
{
    /// <summary>
    /// The easing curves an authored tween channel can use. Engine-free (pure data) so the Core stays
    /// mono/csc-testable. Order is grouped by family as In / Out / InOut so a curve splits arithmetically into
    /// (family, direction) — the evaluator exploits that instead of a thirty-case switch. The order is chosen for
    /// self-consistency, NOT to mirror any tween library's serialized integers (there are no legacy preset assets to
    /// honor). <see cref="Unset"/> and any out-of-range value resolve to <see cref="EaseEvaluator.DefaultEase"/>.
    /// </summary>
    public enum Ease
    {
        /// <summary>No curve chosen yet → resolved to the default (OutQuad) at evaluation time.</summary>
        Unset = 0,

        /// <summary>The identity curve: output equals the normalized time.</summary>
        Linear = 1,

        // Families below are laid out In, Out, InOut — keep this triple grouping (the evaluator relies on it).
        InSine = 2, OutSine, InOutSine,
        InQuad, OutQuad, InOutQuad,
        InCubic, OutCubic, InOutCubic,
        InQuart, OutQuart, InOutQuart,
        InQuint, OutQuint, InOutQuint,
        InExpo, OutExpo, InOutExpo,
        InCirc, OutCirc, InOutCirc,
        InElastic, OutElastic, InOutElastic,
        InBack, OutBack, InOutBack,
        InBounce, OutBounce, InOutBounce,
    }
}
