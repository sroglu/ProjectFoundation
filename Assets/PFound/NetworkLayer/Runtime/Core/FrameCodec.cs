using System;

namespace PFound.NetworkLayer
{
    /// <summary>
    /// A decoded inbound frame: header fields plus a slice pointing at the
    /// still-pooled payload bytes. The slice is only valid until the transport
    /// reclaims its buffer, so consumers deserialize immediately and never stash
    /// the segment.
    /// </summary>
    public readonly struct Envelope
    {
        public readonly MessageKind Kind;
        public readonly ushort Opcode;
        public readonly uint CallToken;
        public readonly ReplyStatus Status;
        public readonly ArraySegment<byte> Payload;

        public Envelope(MessageKind kind, ushort opcode, uint callToken, ReplyStatus status, ArraySegment<byte> payload)
        {
            Kind = kind;
            Opcode = opcode;
            CallToken = callToken;
            Status = status;
            Payload = payload;
        }
    }

    /// <summary>
    /// Turns header fields + a serialized payload into a single self-describing
    /// frame and back. Telepathy already length-prefixes each frame on the socket,
    /// so this layer only encodes the logical header; it never worries about
    /// message boundaries.
    ///
    /// Header layout (uniform [kind][opcode] prefix, little-endian):
    ///   Notify : kind(1) opcode(2)                          | payload
    ///   Request: kind(1) opcode(2) callToken(4)             | payload
    ///   Reply  : kind(1) opcode(2) callToken(4) status(1)   | payload
    ///
    /// Opcode 0 is reserved as a payload-less "control reply" (used for the
    /// server's best-effort expiry notice); real message types never take it.
    /// </summary>
    public static class FrameCodec
    {
        public const ushort ControlOpcode = 0;

        const int NotifyHeader  = 3; // kind + opcode
        const int RequestHeader = 7; // + callToken
        const int ReplyHeader   = 8; // + status

        public static byte[] WriteNotify(ushort opcode, ArraySegment<byte> payload)
        {
            var frame = new byte[NotifyHeader + payload.Count];
            frame[0] = (byte)MessageKind.Notify;
            PutU16(frame, 1, opcode);
            CopyPayload(payload, frame, NotifyHeader);
            return frame;
        }

        public static byte[] WriteRequest(ushort opcode, uint callToken, ArraySegment<byte> payload)
        {
            var frame = new byte[RequestHeader + payload.Count];
            frame[0] = (byte)MessageKind.Request;
            PutU16(frame, 1, opcode);
            PutU32(frame, 3, callToken);
            CopyPayload(payload, frame, RequestHeader);
            return frame;
        }

        public static byte[] WriteReply(ushort opcode, uint callToken, ReplyStatus status, ArraySegment<byte> payload)
        {
            var frame = new byte[ReplyHeader + payload.Count];
            frame[0] = (byte)MessageKind.Reply;
            PutU16(frame, 1, opcode);
            PutU32(frame, 3, callToken);
            frame[7] = (byte)status;
            CopyPayload(payload, frame, ReplyHeader);
            return frame;
        }

        /// <summary>A payload-less reply used to signal expiry/refusal by token.</summary>
        public static byte[] WriteControlReply(uint callToken, ReplyStatus status)
            => WriteReply(ControlOpcode, callToken, status, default);

        public static Envelope Read(ArraySegment<byte> frame)
        {
            byte[] buf = frame.Array;
            int at = frame.Offset;
            var kind = (MessageKind)buf[at];
            ushort opcode = GetU16(buf, at + 1);

            switch (kind)
            {
                case MessageKind.Notify:
                    return new Envelope(kind, opcode, 0, ReplyStatus.Ok,
                        Slice(frame, NotifyHeader));

                case MessageKind.Request:
                    return new Envelope(kind, opcode, GetU32(buf, at + 3), ReplyStatus.Ok,
                        Slice(frame, RequestHeader));

                case MessageKind.Reply:
                    return new Envelope(kind, opcode, GetU32(buf, at + 3), (ReplyStatus)buf[at + 7],
                        Slice(frame, ReplyHeader));

                default:
                    throw new NetworkFault("Unrecognized frame kind byte: 0x" + ((byte)kind).ToString("X2"));
            }
        }

        static ArraySegment<byte> Slice(ArraySegment<byte> frame, int headerLen)
            => new ArraySegment<byte>(frame.Array, frame.Offset + headerLen, frame.Count - headerLen);

        static void CopyPayload(ArraySegment<byte> payload, byte[] dst, int at)
        {
            if (payload.Count > 0)
                Buffer.BlockCopy(payload.Array, payload.Offset, dst, at, payload.Count);
        }

        static void PutU16(byte[] b, int i, ushort v)
        {
            b[i] = (byte)v;
            b[i + 1] = (byte)(v >> 8);
        }

        static void PutU32(byte[] b, int i, uint v)
        {
            b[i] = (byte)v;
            b[i + 1] = (byte)(v >> 8);
            b[i + 2] = (byte)(v >> 16);
            b[i + 3] = (byte)(v >> 24);
        }

        static ushort GetU16(byte[] b, int i)
            => (ushort)(b[i] | (b[i + 1] << 8));

        static uint GetU32(byte[] b, int i)
            => (uint)(b[i] | (b[i + 1] << 8) | (b[i + 2] << 16) | (b[i + 3] << 24));
    }
}
