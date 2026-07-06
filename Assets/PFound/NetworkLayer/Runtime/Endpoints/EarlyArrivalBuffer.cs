#if BACKEND
using System;
using System.Collections.Generic;

namespace PFound.NetworkLayer
{
    /// <summary>
    /// Parks inbound frames that reached the server before their opcode had a handler,
    /// then replays them in arrival order the moment a handler for that opcode
    /// registers — smoothing over the startup window where a peer sends before every
    /// route is wired.
    ///
    /// Design notes:
    ///  - Storage is a single time-ordered list across all opcodes (not a per-opcode
    ///    map): replay is a rare, registration-time scan, so keeping one ordered list
    ///    keeps arrival order exact and eviction trivial.
    ///  - Each parked entry copies the payload bytes, because the pump's slice points
    ///    at a buffer the transport reclaims after dispatch.
    ///  - Bounded by frame count; on overflow the OLDEST parked frame is dropped
    ///    (a stale early frame is the least valuable). Evictions are counted so a host
    ///    can alarm if the buffer is chronically saturated.
    /// </summary>
    public sealed class EarlyArrivalBuffer
    {
        public struct Parked
        {
            public int Peer;
            public MessageKind Kind;
            public ushort Opcode;
            public uint CallToken;
            public byte[] Body;
            public long ArrivedMs;
        }

        readonly List<Parked> _parked = new List<Parked>();
        readonly int _capacity;

        public long EvictionCount { get; private set; }
        public int Count => _parked.Count;

        public EarlyArrivalBuffer(int capacity)
        {
            _capacity = capacity;
        }

        /// <summary>Copy and park a frame whose opcode currently has no handler.</summary>
        public void Park(int peer, MessageKind kind, ushort opcode, uint callToken, ArraySegment<byte> body, long nowMs)
        {
            if (_parked.Count >= _capacity)
            {
                _parked.RemoveAt(0); // drop oldest
                EvictionCount++;
            }

            var copy = new byte[body.Count];
            if (body.Count > 0)
                Buffer.BlockCopy(body.Array, body.Offset, copy, 0, body.Count);

            _parked.Add(new Parked
            {
                Peer = peer,
                Kind = kind,
                Opcode = opcode,
                CallToken = callToken,
                Body = copy,
                ArrivedMs = nowMs,
            });
        }

        /// <summary>Hand back (and remove) every parked frame for <paramref name="opcode"/>
        /// in arrival order, so a freshly-registered handler processes them at once.</summary>
        public void DrainTo(ushort opcode, Action<Parked> deliver)
        {
            // Single forward pass preserves arrival order; compact in place.
            int write = 0;
            var replay = new List<Parked>();
            for (int read = 0; read < _parked.Count; read++)
            {
                if (_parked[read].Opcode == opcode)
                    replay.Add(_parked[read]);
                else
                    _parked[write++] = _parked[read];
            }
            _parked.RemoveRange(write, _parked.Count - write);

            for (int i = 0; i < replay.Count; i++)
                deliver(replay[i]);
        }
    }
}
#endif
