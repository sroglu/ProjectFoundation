using System;
using System.Collections.Generic;
using MessagePack;
using MessagePack.Resolvers;

namespace PFound.NetworkLayer
{
    /// <summary>
    /// Production <see cref="IBodyCodec"/> backed by MessagePack-CSharp's official AOT source generator.
    /// It serializes the envelope's <b>payload DTO</b> (never the envelope itself): every wire type is a
    /// first-party <c>[MessagePackObject]</c> DTO whose formatter is generated at compile time, so there is
    /// zero runtime IL emit and zero reflection-based resolver discovery — safe under IL2CPP/iOS.
    ///
    /// A host pushes its assembly's generated resolver (the one anchored by
    /// <c>[MessagePack.GeneratedMessagePackResolver]</c>) into the constructor; the codec composes those ahead
    /// of <see cref="BuiltinResolver"/> (primitives, string, arrays, common collections). The composite uses
    /// ONLY generated + builtin — no <c>StandardResolver</c>/<c>ContractlessStandardResolver</c>/dynamic
    /// resolver, so nothing falls back to runtime code generation.
    /// </summary>
    public sealed class MessagePackBodyCodec : IBodyCodec
    {
        readonly MessagePackSerializerOptions _options;

        /// <summary>
        /// Build the codec over the generated resolver(s) a host supplies — one per DTO-bearing assembly,
        /// each the <c>Instance</c> of a <c>[GeneratedMessagePackResolver]</c> anchor. They are composed in
        /// order, then <see cref="BuiltinResolver"/> as the primitive/collection catch-all.
        /// </summary>
        public MessagePackBodyCodec(params IFormatterResolver[] generatedResolvers)
        {
            var resolvers = new List<IFormatterResolver>(generatedResolvers.Length + 1);
            resolvers.AddRange(generatedResolvers);
            resolvers.Add(BuiltinResolver.Instance);
            _options = MessagePackSerializerOptions.Standard
                .WithResolver(CompositeResolver.Create(resolvers.ToArray()));
        }

        public byte[] Pack(Message message)
        {
            var carrier = (IMessagePayload)message;
            return MessagePackSerializer.Serialize(carrier.PayloadType, carrier.Payload, _options);
        }

        public Message Unpack(Type type, ArraySegment<byte> body)
        {
            var message = (Message)Activator.CreateInstance(type);
            var carrier = (IMessagePayload)message;
            carrier.Payload = MessagePackSerializer.Deserialize(
                carrier.PayloadType, new ReadOnlyMemory<byte>(body.Array, body.Offset, body.Count), _options);
            return message;
        }
    }
}
