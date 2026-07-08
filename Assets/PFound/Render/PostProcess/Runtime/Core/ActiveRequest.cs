namespace PFound.Render.PostProcess
{
    /// <summary>
    /// Read-only snapshot of an active request in an adapter's stack. The service rebuilds the
    /// snapshot list each tick; adapters MUST NOT cache the list.
    /// </summary>
    /// <typeparam name="TRequest">The strongly-typed payload struct.</typeparam>
    public readonly struct ActiveRequest<TRequest> where TRequest : struct
    {
        /// <summary>The original request payload.</summary>
        public readonly TRequest Request;

        /// <summary>Caller-supplied priority used by <see cref="PostProcessBlendPolicy.HighestPriorityWins"/>.</summary>
        public readonly int Priority;

        /// <summary>Fade weight in [0, 1] computed from the request's fade-in/fade-out timers.</summary>
        public readonly float FadeWeight;

        /// <summary>Service-assigned monotonic ticket id. Diagnostic; not normally used by adapters.</summary>
        public readonly long TicketId;

        /// <summary>Constructs a snapshot entry.</summary>
        public ActiveRequest(TRequest request, int priority, float fadeWeight, long ticketId)
        {
            Request = request;
            Priority = priority;
            FadeWeight = fadeWeight;
            TicketId = ticketId;
        }
    }
}
