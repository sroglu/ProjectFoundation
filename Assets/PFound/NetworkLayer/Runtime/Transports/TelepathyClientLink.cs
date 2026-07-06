using System;
using PFound.NetworkLayer.Telepathy;

namespace PFound.NetworkLayer
{
    /// <summary>
    /// Adapts the vendored MIT Telepathy TCP client to this layer's
    /// <see cref="IClientLink"/> contract. Telepathy owns the sockets, threads, and
    /// length-prefix framing; this wrapper only re-shapes its callback surface into
    /// the events the messaging core expects and forwards the pump. No TCP or
    /// framing logic is reimplemented here.
    /// </summary>
    public sealed class TelepathyClientLink : IClientLink
    {
        readonly TelepathyClient _tcp;

        public event Action Opened;
        public event Action Closed;
        public event Action<ArraySegment<byte>> Received;

        public bool IsOpen => _tcp.Connected;

        public TelepathyClientLink(ClientLinkOptions options)
        {
            // Receive timeout 0 = disabled: this layer enforces liveness with its own
            // application-level call deadlines rather than a raw socket read timeout.
            _tcp = new TelepathyClient(options.NoDelay, options.MaxFrameBytes, options.SendTimeoutMs, 0);
            // Backpressure: bound the send/receive pipes so a stalled socket sheds the
            // connection instead of growing memory and latency without limit.
            _tcp.SendQueueLimit = options.SendQueueLimit;
            _tcp.ReceiveQueueLimit = options.ReceiveQueueLimit;
            _tcp.OnConnected = () => Opened?.Invoke();
            _tcp.OnData = seg => Received?.Invoke(seg);
            _tcp.OnDisconnected = () => Closed?.Invoke();
        }

        public void Open(string host, int port) => _tcp.Connect(host, port);
        public void Close() => _tcp.Disconnect();
        public bool Deliver(ArraySegment<byte> frame) => _tcp.Send(frame);
        public void Pump(int budget) => _tcp.Tick(budget);
    }
}
