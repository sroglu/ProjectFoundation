#if BACKEND
namespace PFound.NetworkLayer
{
    /// <summary>
    /// Non-generic handle the server keeps for a request whose reply is deferred
    /// (the handler chose to answer later — e.g. after an async DB hit). The
    /// watchdog scans these for expiry; carries just enough to reply-by-token and
    /// recycle the request buffer.
    /// </summary>
    public abstract class RequestExchangeBase
    {
        internal int Peer;
        internal uint CallToken;
        internal ushort Opcode;
        internal long StartedMs;
        internal long DeadlineMs;
        internal bool Answered;
        internal Message RawRequest;
    }

    /// <summary>
    /// Passed to a deferred request handler. Read <see cref="Request"/>, do the work
    /// (possibly across ticks), then call <see cref="Reply"/> exactly once. If the
    /// server's serve-deadline elapses first, the watchdog reclaims the exchange and
    /// a later <see cref="Reply"/> is silently ignored.
    /// </summary>
    public sealed class RequestExchange<TRequest> : RequestExchangeBase
        where TRequest : RequestMessage
    {
        ServerPeer _server;
        public TRequest Request { get; internal set; }

        internal void Bind(ServerPeer server) => _server = server;

        public void Reply(ReplyMessage reply) => _server.FulfillDeferred(this, reply);
    }
}
#endif
