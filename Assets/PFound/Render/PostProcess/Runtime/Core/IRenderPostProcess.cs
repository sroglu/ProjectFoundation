namespace PFound.Render.PostProcess
{
    /// <summary>
    /// Phase 6 PostProcess orchestration service. Gameplay/UI code issues typed requests; the
    /// service routes each to the registered <see cref="IRenderPostProcessAdapter{TRequest}"/>
    /// and ticks all adapters once per frame via <c>PFound.LoopScheduler</c> BeforeRender.
    /// </summary>
    public interface IRenderPostProcess
    {
        /// <summary>
        /// Submits a request for the effect bound to <typeparamref name="TRequest"/>. Returns a
        /// ticket the caller MUST keep until they want the effect to stop. Releasing the ticket
        /// (or disposing it) removes the request from the stack on the next tick.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">No adapter is registered for <typeparamref name="TRequest"/>.</exception>
        /// <exception cref="System.ObjectDisposedException">The service has been disposed.</exception>
        IRenderPostProcessTicket Request<TRequest>(TRequest request, int priority = 0)
            where TRequest : struct;
    }
}
