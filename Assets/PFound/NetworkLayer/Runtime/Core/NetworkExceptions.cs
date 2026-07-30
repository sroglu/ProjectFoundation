using System;

namespace PFound.NetworkLayer
{
    /// <summary>Base for faults surfaced to an awaiting RPC caller.</summary>
    public class NetworkFault : Exception
    {
        public NetworkFault(string message) : base(message) { }
    }

    /// <summary>An RPC call passed its client-side deadline with no reply.</summary>
    public sealed class CallExpiredFault : NetworkFault
    {
        public CallExpiredFault() : base("RPC call expired before a reply arrived.") { }
    }

    /// <summary>The connection dropped while a call was still outstanding.</summary>
    public sealed class LinkDroppedFault : NetworkFault
    {
        public LinkDroppedFault() : base("Link closed before the reply arrived.") { }
    }

    /// <summary>The peer answered with a non-success status carried in the frame.</summary>
    public sealed class RemoteRejectedFault : NetworkFault
    {
        public ReplyStatus Status { get; }
        public RemoteRejectedFault(ReplyStatus status)
            : base("Remote endpoint rejected the request: " + status)
        {
            Status = status;
        }
    }

    /// <summary>
    /// The reply frame for a specific call could not be decoded (corrupt, oversized,
    /// or schema-mismatched body). Surfaced to the single waiting caller so a bad
    /// reply faults only that call — never the whole connection (see decode isolation
    /// around the receive pump).
    /// </summary>
    public sealed class MalformedFrameFault : NetworkFault
    {
        public MalformedFrameFault(string detail)
            : base("Inbound frame could not be decoded: " + detail) { }
    }

    /// <summary>An awaitable connect attempt did not reach the connected state before
    /// its deadline elapsed.</summary>
    public sealed class ConnectTimeoutFault : NetworkFault
    {
        public ConnectTimeoutFault() : base("Connect attempt timed out before the link opened.") { }
    }
}
