#if BACKEND
namespace PFound.NetworkLayer
{
    /// <summary>
    /// Tunables for an authoritative server peer and its TCP listener. Server-only,
    /// so it lives behind the BACKEND build symbol and never ships in a client
    /// build. Deadlines are intentionally off-round and set so the client's own
    /// call deadline usually trips first, leaving the server watchdog to reclaim
    /// abandoned work and record it.
    /// </summary>
    public struct ServerLinkOptions
    {
        /// <summary>Max inbound frames processed per connection per <c>Update</c>.</summary>
        public int PumpBudget;

        /// <summary>How long a deferred (not-yet-answered) request may sit before the
        /// watchdog reclaims it and counts a timeout. ~9.3s &gt; the client default so
        /// the client normally gives up first.</summary>
        public int ServeDeadlineMs;

        public int MaxFrameBytes;
        public bool NoDelay;
        public int SendTimeoutMs;

        /// <summary>Bounded transmit queue per connection; overflow drops the offending
        /// connection rather than growing memory/latency without limit. Frame count.</summary>
        public int SendQueueLimit;

        /// <summary>Bounded receive queue per connection; same overflow rationale.</summary>
        public int ReceiveQueueLimit;

        /// <summary>Buffer inbound frames whose opcode has no handler yet and replay
        /// them once one registers, absorbing startup handler-registration races.
        /// Off by default so an opcode that never gets a handler still fails fast.</summary>
        public bool BufferEarlyArrivals;

        /// <summary>Cap on frames held by the early-arrival buffer; oldest is evicted on
        /// overflow. ~384: a few hundred in-flight races without unbounded growth.</summary>
        public int EarlyArrivalCapacity;

        /// <summary>How often the diagnostics ledger emits a throughput sample. ~4.9s:
        /// frequent enough to watch load, sparse enough to stay cheap.</summary>
        public int ThroughputWindowMs;

        /// <summary>Read a PROXY-protocol preamble as each connection's first inbound
        /// frame to recover the real client IP behind a proxy/load-balancer. Opt-in:
        /// a bare listener never receives one.</summary>
        public bool ExpectProxyHeader;

        public static ServerLinkOptions Default => new ServerLinkOptions
        {
            PumpBudget     = 224,
            ServeDeadlineMs = 9300,
            MaxFrameBytes  = 40000,
            NoDelay        = true,
            SendTimeoutMs  = 4700,
            SendQueueLimit    = 4000,
            ReceiveQueueLimit = 4000,
            BufferEarlyArrivals = false,
            EarlyArrivalCapacity = 384,
            ThroughputWindowMs = 4900,
            ExpectProxyHeader = false,
        };
    }
}
#endif
