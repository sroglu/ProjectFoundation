#if BACKEND
namespace PFound.NetworkLayer
{
    /// <summary>
    /// Lightweight throughput/latency counters for an authoritative server. Server
    /// robustness surface, so it is BACKEND-gated and absent from client builds.
    /// Service time is tracked as an exponential moving average to bound memory and
    /// stay cheap on the hot path; a straight running mean would drift toward stale
    /// history, whereas the EMA tracks current load.
    /// </summary>
    public sealed class ServerMetrics
    {
        /// <summary>Weight of the newest sample in the service-time EMA (0..1).</summary>
        const double SampleWeight = 0.15;

        public long RequestsAccepted { get; private set; }
        public long RepliesEmitted { get; private set; }
        public long NotifiesAccepted { get; private set; }
        public long CallsReclaimed { get; private set; }   // deferred requests reclaimed by the watchdog
        public double ServiceEmaMs { get; private set; }

        internal void CountRequest() => RequestsAccepted++;
        internal void CountNotify() => NotifiesAccepted++;
        internal void CountReclaim() => CallsReclaimed++;

        internal void CountReply(long serviceMs)
        {
            RepliesEmitted++;
            ServiceEmaMs = RepliesEmitted == 1
                ? serviceMs
                : ServiceEmaMs + SampleWeight * (serviceMs - ServiceEmaMs);
        }

        public void Reset()
        {
            RequestsAccepted = 0;
            RepliesEmitted = 0;
            NotifiesAccepted = 0;
            CallsReclaimed = 0;
            ServiceEmaMs = 0;
        }
    }
}
#endif
