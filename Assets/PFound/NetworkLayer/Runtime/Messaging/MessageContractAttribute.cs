using System;

namespace PFound.NetworkLayer
{
    /// <summary>
    /// Optional convenience for binding a message type to its opcode declaratively:
    /// <code>[MessageContract(1042)] public sealed class MoveRequest : RequestMessage {}</code>
    /// A catalog can harvest these via reflection (<see cref="MessageCatalog.HarvestContracts"/>),
    /// or you can register opcodes explicitly and skip the attribute entirely.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class MessageContractAttribute : Attribute
    {
        public ushort Opcode { get; }
        public MessageContractAttribute(ushort opcode) => Opcode = opcode;
    }
}
