namespace PFound.HubApp.Tutorial
{
    /// <summary>
    /// Neutral hook the hub uses to invite an optional tutorial layer for a mini-game.
    /// The hub calls <see cref="Boot"/> when a mini-game lobby mounts; if a Tutorial
    /// assembly is present in the build, it registered an implementation via
    /// ServiceRegistry — otherwise <see cref="ServiceRegistry"/> returns null and the
    /// hub continues without a tutorial.
    /// </summary>
    /// <remarks>
    /// **Architectural intent (see Camping FR-014a + memory rule
    /// `feedback-tutorial-decoupling`):** the gameplay assembly of a mini-game MUST
    /// NOT reference its Tutorial assembly. Tutorial code subscribes to gameplay
    /// signals and drives gameplay through public APIs only. This interface is the
    /// **only** type the hub itself touches — it knows nothing about which tutorials
    /// (if any) are wired.
    ///
    /// Implementations live in per-mini-game tutorial asmdefs (e.g.
    /// `Playnest.MiniGames.Camping.Tutorial`). Implementations register themselves
    /// with `ServiceRegistry` (or equivalent locator) at editor/runtime
    /// initialization; the hub resolves the interface and dispatches Boot calls.
    ///
    /// Multiple implementations can coexist — the hub fires <see cref="Boot"/> for
    /// each, and each implementation early-returns if <paramref name="miniGameId"/>
    /// is not its own.
    /// </remarks>
    public interface ITutorialBootstrap
    {
        /// <summary>
        /// Called by the hub when a mini-game lobby mounts. The implementation
        /// inspects <paramref name="miniGameId"/>, decides whether this tutorial is
        /// relevant, queries its own first-run state via save / vault, and (if
        /// applicable) wires its signal subscriptions + step controller.
        /// </summary>
        /// <param name="miniGameId">Stable lower_snake_case id, e.g. <c>"camping"</c>.</param>
        void Boot(string miniGameId);
    }
}
