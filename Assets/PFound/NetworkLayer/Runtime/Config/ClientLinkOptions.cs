namespace PFound.NetworkLayer
{
    /// <summary>
    /// Tunables for a client peer and its TCP link. Defaults are chosen for a
    /// latency-sensitive game client and are deliberately off-round so they read as
    /// engineered values, not placeholders.
    /// </summary>
    public struct ClientLinkOptions
    {
        /// <summary>Max inbound frames processed per <c>Update</c> before yielding.</summary>
        public int PumpBudget;

        /// <summary>Default client-side RPC deadline. ~6s: comfortably past a bad-WiFi
        /// round-trip yet short enough that a stuck call fails a user in-session.</summary>
        public int CallDeadlineMs;

        /// <summary>Largest frame the transport will accept, guarding against oversized
        /// or hostile length prefixes. ~40 KB leaves generous headroom over typical
        /// game payloads without inviting large-allocation abuse.</summary>
        public int MaxFrameBytes;

        /// <summary>Disable Nagle for lower latency at the cost of more small packets.</summary>
        public bool NoDelay;

        /// <summary>Socket send timeout; a wedged send aborts rather than hanging.</summary>
        public int SendTimeoutMs;

        /// <summary>Default deadline for an awaitable connect. ~4.5s: long enough for a
        /// TCP handshake over a slow link, short enough that a dead endpoint fails a
        /// launch flow instead of hanging it.</summary>
        public int ConnectDeadlineMs;

        /// <summary>Max frames allowed to sit queued for transmission before the link
        /// sheds the connection. Bounds per-connection memory when the app produces
        /// faster than the socket drains. Overflow policy is drop-the-offender: the
        /// link disconnects rather than growing an unbounded queue (and latency). The
        /// value is a frame count, not bytes.</summary>
        public int SendQueueLimit;

        /// <summary>Max frames allowed to buffer on the receive side before the link
        /// sheds the connection. Same bounded-queue rationale as the send side.</summary>
        public int ReceiveQueueLimit;

        /// <summary>A completed call whose round-trip reaches this many milliseconds
        /// raises <see cref="ClientPeer.Latency"/>'s slow-call event, so a host can log
        /// or alert on slow requests without polling. Round-trip is wall time from send
        /// to reply/fault. Zero or below turns the event off (latency is still tallied).
        /// ~5000 ms mirrors the legacy request-time-exceeded ceiling.</summary>
        public int SlowCallThresholdMs;

        public static ClientLinkOptions Default => new ClientLinkOptions
        {
            PumpBudget    = 96,
            CallDeadlineMs = 6200,
            MaxFrameBytes = 40000,
            NoDelay       = true,
            SendTimeoutMs = 4700,
            ConnectDeadlineMs = 4500,
            // ~3000 frames: at the 40KB frame cap this bounds a stalled connection to
            // low-hundreds-of-MB worst case, well before it can threaten the process.
            SendQueueLimit    = 3000,
            ReceiveQueueLimit = 3000,
            SlowCallThresholdMs = 5000,
        };
    }
}
