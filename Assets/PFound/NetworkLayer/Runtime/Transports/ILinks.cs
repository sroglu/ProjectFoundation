using System;

namespace PFound.NetworkLayer
{
    /// <summary>
    /// The client end of a transport, decoupled from the messaging core. The core
    /// only ever sees opaque byte frames in and out — it neither knows nor cares
    /// whether the bytes ride TCP, an in-memory pipe, or a latency shim. Inbound
    /// frames surface on <see cref="Received"/> only while the host drains the
    /// transport via <see cref="Pump"/>, keeping all delivery on one thread.
    /// </summary>
    public interface IClientLink
    {
        event Action Opened;
        event Action Closed;
        event Action<ArraySegment<byte>> Received;

        bool IsOpen { get; }

        void Open(string host, int port);
        void Close();

        /// <summary>Queue a frame for transmission. Returns false if it could not be accepted.</summary>
        bool Deliver(ArraySegment<byte> frame);

        /// <summary>Drain up to <paramref name="budget"/> inbound frames, raising events for each.</summary>
        void Pump(int budget);
    }

    /// <summary>
    /// The server end of a transport. Peers are identified by an integer key minted
    /// by the transport; the messaging core treats it as an opaque handle. Same
    /// pump-driven, single-threaded delivery contract as the client link.
    /// </summary>
    public interface IServerLink
    {
        event Action<int> PeerArrived;
        event Action<int> PeerDeparted;
        event Action<int, ArraySegment<byte>> Received;

        bool IsListening { get; }

        void Listen(int port);
        void Halt();

        bool Deliver(int peer, ArraySegment<byte> frame);
        void Evict(int peer);

        /// <summary>The transport-observed remote address of a peer (the address the
        /// socket actually connected from). When a PROXY front is in play this is the
        /// proxy's own address; the messaging layer recovers the real origin from the
        /// PROXY preamble on top of this.</summary>
        string AddressOf(int peer);

        void Pump(int budget);
    }
}
