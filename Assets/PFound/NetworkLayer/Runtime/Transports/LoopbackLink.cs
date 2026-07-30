using System;
using System.Collections.Generic;

namespace PFound.NetworkLayer
{
    /// <summary>
    /// The shared meeting point for an in-memory client/server transport pair. It
    /// holds two frame queues (one per direction) and the connect/disconnect edges,
    /// so a full request→reply round-trip runs deterministically with no sockets and
    /// no threads — advance both ends by pumping. Frames are copied on hand-off so a
    /// pooled sender buffer can never be observed after it is reused.
    /// </summary>
    public sealed class LoopbackHub
    {
        public const int PeerKey = 1;

        internal readonly Queue<byte[]> InboundToServer = new Queue<byte[]>();
        internal readonly Queue<byte[]> InboundToClient = new Queue<byte[]>();

        internal bool ConnectPending;   // client opened, server hasn't observed yet
        internal bool DisconnectPending; // client closed, server hasn't observed yet
        internal bool PeerLive;          // server currently regards the client as joined

        /// <summary>The address the in-memory server reports for the peer. Defaults to
        /// an RFC-5737 TEST-NET-3 address so tests read as clearly synthetic; a test
        /// may set it to model a specific origin.</summary>
        public string RawPeerAddress = "203.0.113.7";
    }

    public sealed class LoopbackClientLink : IClientLink
    {
        readonly LoopbackHub _hub;

        public event Action Opened;
        public event Action Closed;
        public event Action<ArraySegment<byte>> Received;

        public bool IsOpen { get; private set; }

        public LoopbackClientLink(LoopbackHub hub) => _hub = hub;

        public void Open(string host, int port)
        {
            IsOpen = true;
            _hub.ConnectPending = true;
            Opened?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            _hub.DisconnectPending = true;
            Closed?.Invoke();
        }

        public bool Deliver(ArraySegment<byte> frame)
        {
            if (!IsOpen) return false;
            _hub.InboundToServer.Enqueue(Copy(frame));
            return true;
        }

        public void Pump(int budget)
        {
            for (int i = 0; i < budget && _hub.InboundToClient.Count > 0; i++)
            {
                var frame = _hub.InboundToClient.Dequeue();
                Received?.Invoke(new ArraySegment<byte>(frame));
            }
        }

        static byte[] Copy(ArraySegment<byte> frame)
        {
            var clone = new byte[frame.Count];
            Buffer.BlockCopy(frame.Array, frame.Offset, clone, 0, frame.Count);
            return clone;
        }
    }

    public sealed class LoopbackServerLink : IServerLink
    {
        readonly LoopbackHub _hub;

        public event Action<int> PeerArrived;
        public event Action<int> PeerDeparted;
        public event Action<int, ArraySegment<byte>> Received;

        public bool IsListening { get; private set; }

        public LoopbackServerLink(LoopbackHub hub) => _hub = hub;

        public void Listen(int port) => IsListening = true;

        public void Halt()
        {
            IsListening = false;
            if (_hub.PeerLive)
            {
                _hub.PeerLive = false;
                PeerDeparted?.Invoke(LoopbackHub.PeerKey);
            }
        }

        public bool Deliver(int peer, ArraySegment<byte> frame)
        {
            if (!_hub.PeerLive) return false;
            _hub.InboundToClient.Enqueue(Copy(frame));
            return true;
        }

        public void Evict(int peer)
        {
            if (!_hub.PeerLive) return;
            _hub.PeerLive = false;
            PeerDeparted?.Invoke(peer);
        }

        public string AddressOf(int peer) => _hub.RawPeerAddress;

        public void Pump(int budget)
        {
            if (!IsListening) return;

            if (_hub.ConnectPending && !_hub.PeerLive)
            {
                _hub.ConnectPending = false;
                _hub.PeerLive = true;
                PeerArrived?.Invoke(LoopbackHub.PeerKey);
            }

            for (int i = 0; i < budget && _hub.InboundToServer.Count > 0; i++)
            {
                var frame = _hub.InboundToServer.Dequeue();
                Received?.Invoke(LoopbackHub.PeerKey, new ArraySegment<byte>(frame));
            }

            if (_hub.DisconnectPending && _hub.PeerLive)
            {
                _hub.DisconnectPending = false;
                _hub.PeerLive = false;
                PeerDeparted?.Invoke(LoopbackHub.PeerKey);
            }
        }

        static byte[] Copy(ArraySegment<byte> frame)
        {
            var clone = new byte[frame.Count];
            Buffer.BlockCopy(frame.Array, frame.Offset, clone, 0, frame.Count);
            return clone;
        }
    }
}
