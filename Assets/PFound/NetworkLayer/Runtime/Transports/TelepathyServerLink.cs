#if BACKEND
using System;
using PFound.NetworkLayer.Telepathy;

namespace PFound.NetworkLayer
{
    /// <summary>
    /// Adapts the vendored MIT Telepathy TCP server to this layer's
    /// <see cref="IServerLink"/> contract. Server-only, so it is BACKEND-gated in
    /// step with the vendored server it wraps. Telepathy supplies the accept loop,
    /// per-connection threads, and framing; this wrapper maps its integer connection
    /// ids and callbacks onto the transport-agnostic events the messaging core uses.
    /// </summary>
    public sealed class TelepathyServerLink : IServerLink
    {
        readonly TelepathyServer _tcp;

        public event Action<int> PeerArrived;
        public event Action<int> PeerDeparted;
        public event Action<int, ArraySegment<byte>> Received;

        readonly bool _proxyFront;

        public bool IsListening => _tcp.Active;

        public TelepathyServerLink(ServerLinkOptions options)
        {
            _tcp = new TelepathyServer(options.NoDelay, options.MaxFrameBytes, options.SendTimeoutMs, 0);
            // Backpressure: bound the per-connection send/receive pipes.
            _tcp.SendQueueLimit = options.SendQueueLimit;
            _tcp.ReceiveQueueLimit = options.ReceiveQueueLimit;
            _proxyFront = options.ExpectProxyHeader;
            _tcp.OnConnected = peer => PeerArrived?.Invoke(peer);
            _tcp.OnData = (peer, seg) => Received?.Invoke(peer, seg);
            _tcp.OnDisconnected = peer => PeerDeparted?.Invoke(peer);
        }

        public void Listen(int port) => _tcp.Start(port, _proxyFront);
        public void Halt() => _tcp.Stop();

        public bool Deliver(int peer, ArraySegment<byte> frame) => _tcp.Send(peer, frame);
        public void Evict(int peer) => _tcp.Disconnect(peer);
        public string AddressOf(int peer) => _tcp.GetClientAddress(peer);
        public void Pump(int budget) => _tcp.Tick(budget);
    }
}
#endif
