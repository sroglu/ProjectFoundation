namespace PFound.InputRouter
{
    /// <summary>
    /// The four states an intent moves through as its underlying input is polled tick after
    /// tick. Derived purely from the active/inactive edge between the previous and current tick:
    ///
    ///   was=false, now=false -> <see cref="Idle"/>
    ///   was=false, now=true  -> <see cref="Started"/>   (rising edge)
    ///   was=true,  now=true  -> <see cref="Held"/>      (level while active)
    ///   was=true,  now=false -> <see cref="Ended"/>     (falling edge)
    ///
    /// Subscribers choose exactly one phase to be notified on, so a "began" handler and an
    /// "ended" handler stay cleanly separated.
    /// </summary>
    public enum IntentPhase
    {
        /// <summary>Input has been inactive since at least the previous tick.</summary>
        Idle = 0,

        /// <summary>Rising edge: input became active this tick.</summary>
        Started = 1,

        /// <summary>Input has stayed active across ticks.</summary>
        Held = 2,

        /// <summary>Falling edge: input became inactive this tick.</summary>
        Ended = 3
    }
}
