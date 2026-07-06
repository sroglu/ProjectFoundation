using System;
using MessagePack;
using MessagePack.Resolvers;

namespace PFound.NetworkLayer
{
    /// <summary>
    /// Production <see cref="IBodyCodec"/> backed by the vendored MessagePack-CSharp.
    /// Uses the contractless resolver so game message types need no MessagePack
    /// attributes — plain public fields just work. Lives outside the csc/mono test
    /// build (MessagePack pulls in netstandard/System.Memory facades that a bare mono
    /// lacks); Unity references the assembly directly, so it compiles there. The
    /// framing/dispatch core is codec-agnostic, so swapping this in changes nothing
    /// but the payload bytes.
    /// </summary>
    public sealed class MessagePackBodyCodec : IBodyCodec
    {
        static readonly MessagePackSerializerOptions Options =
            ContractlessStandardResolver.Options;

        public byte[] Pack(Message message)
            => MessagePackSerializer.Serialize(message.GetType(), message, Options);

        public Message Unpack(Type type, ArraySegment<byte> body)
            => (Message)MessagePackSerializer.Deserialize(
                type, new ReadOnlyMemory<byte>(body.Array, body.Offset, body.Count), Options);
    }
}
