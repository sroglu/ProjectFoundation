using System;

namespace PFound.NetworkLayer
{
    /// <summary>
    /// Pluggable payload (de)serializer. The messaging core owns framing and
    /// dispatch but stays agnostic about how a message body turns into bytes, so
    /// the heavy MessagePack dependency lives behind this seam and the pure core
    /// stays csc/mono-compilable and testable with a lightweight codec.
    /// </summary>
    public interface IBodyCodec
    {
        /// <summary>Serialize a message's payload fields to a fresh byte buffer.</summary>
        byte[] Pack(Message message);

        /// <summary>Materialize a message of the given type from a payload slice.</summary>
        Message Unpack(Type type, ArraySegment<byte> body);
    }
}
