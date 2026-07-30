namespace PFound.MVC.core
{
    /// <summary>
    /// Type-safe event handler interface for MVC broadcasts.
    /// Controllers implement this to receive strongly-typed events
    /// without string-based routing or EventArgs casting.
    /// </summary>
    /// <typeparam name="TEvent">Event payload type (struct recommended)</typeparam>
    public interface IEventHandler<in TEvent>
    {
        void Handle(TEvent evt);
    }
}
