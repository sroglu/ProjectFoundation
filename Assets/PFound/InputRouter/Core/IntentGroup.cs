namespace PFound.InputRouter
{
    /// <summary>
    /// Coarse routing category for an intent. The router can silence the whole
    /// <see cref="Gameplay"/> group (and the injected UI gate only ever affects that group),
    /// while <see cref="System"/> intents keep firing so menu / pause style actions survive a
    /// gameplay freeze.
    /// </summary>
    public enum IntentGroup
    {
        /// <summary>In-world actions; suppressed when gameplay is gated off or the pointer is over UI.</summary>
        Gameplay = 0,

        /// <summary>Meta actions (pause, menu, debug); only the global switch can silence these.</summary>
        System = 1
    }
}
