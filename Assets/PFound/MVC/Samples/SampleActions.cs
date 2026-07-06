namespace PFound.MVC.Samples
{
    /// <summary>
    /// Type-safe event structs for MVC sample broadcasts.
    /// Replaces string-based action constants with strongly-typed events
    /// used via <see cref="core.MvcContext.Broadcast{TEvent}"/>.
    /// </summary>
    public readonly struct CounterChangedEvent
    {
        public readonly int NewValue;
        public CounterChangedEvent(int newValue) => NewValue = newValue;
    }
}
