using System;
using System.Collections.Generic;
using System.Reflection;

namespace PFound.NetworkLayer
{
    /// <summary>
    /// The single source of truth binding opcodes to message types, plus the
    /// per-type free lists that make messages reusable. Both peers share a catalog
    /// so the sender's opcode maps to the receiver's type. Registration is explicit
    /// by default (deterministic, reflection-free); <see cref="HarvestContracts"/>
    /// offers an attribute-driven shortcut when a project prefers it.
    /// </summary>
    public sealed class MessageCatalog
    {
        readonly Dictionary<ushort, Type> _byOpcode = new Dictionary<ushort, Type>();
        readonly Dictionary<Type, ushort> _byType = new Dictionary<Type, ushort>();
        readonly Dictionary<Type, Stack<Message>> _freeLists = new Dictionary<Type, Stack<Message>>();
        readonly IBodyCodec _codec;

        public MessageCatalog(IBodyCodec codec)
        {
            _codec = codec;
        }

        public void Enroll<T>(ushort opcode) where T : Message, new()
        {
            if (opcode == FrameCodec.ControlOpcode)
                throw new NetworkFault("Opcode 0 is reserved for control frames and cannot be enrolled.");
            if (_byOpcode.ContainsKey(opcode))
                throw new NetworkFault("Opcode " + opcode + " is already enrolled to " + _byOpcode[opcode].Name + ".");

            _byOpcode.Add(opcode, typeof(T));
            _byType.Add(typeof(T), opcode);
            _freeLists.Add(typeof(T), new Stack<Message>());
        }

        /// <summary>Enroll every <see cref="MessageContractAttribute"/>-tagged message in an assembly.</summary>
        public void HarvestContracts(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (!typeof(Message).IsAssignableFrom(type) || type.IsAbstract)
                    continue;
                var contract = (MessageContractAttribute)Attribute.GetCustomAttribute(type, typeof(MessageContractAttribute));
                if (contract == null)
                    continue;

                _byOpcode.Add(contract.Opcode, type);
                _byType.Add(type, contract.Opcode);
                _freeLists.Add(type, new Stack<Message>());
            }
        }

        public ushort OpcodeFor(Type type) => _byType[type];
        public Type TypeFor(ushort opcode) => _byOpcode[opcode];

        // --- pooling ---------------------------------------------------------

        public T Take<T>() where T : Message, new()
        {
            var stack = _freeLists[typeof(T)];
            return stack.Count > 0 ? (T)stack.Pop() : new T();
        }

        public void Recycle(Message message)
        {
            message.Clear();
            _freeLists[message.GetType()].Push(message);
        }

        // --- serialization glue ---------------------------------------------

        public byte[] PackBody(Message message) => _codec.Pack(message);

        public Message UnpackByOpcode(ushort opcode, ArraySegment<byte> body)
            => _codec.Unpack(_byOpcode[opcode], body);
    }
}
