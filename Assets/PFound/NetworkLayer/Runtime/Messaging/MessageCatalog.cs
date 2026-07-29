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

        /// <summary>
        /// Convenience overload: enroll against a per-domain opcode <see cref="Enum"/> declared with
        /// explicit values (the recommended way to keep opcodes typed, stable, and self-documenting).
        /// The enum value is narrowed to the wire <c>ushort</c> — an out-of-range value throws
        /// (fail-fast). A request and its reply are distinct catalog entries, so give each its own value.
        /// </summary>
        public void Enroll<T>(Enum opcode) where T : Message, new()
            => Enroll<T>(Convert.ToUInt16(opcode));

        /// <summary>
        /// Convenience overload for the banded two-level opcode scheme: compose the wire opcode from a
        /// central <c>domain</c> enum (high byte) and a per-domain <c>op</c> enum (low byte) via
        /// <see cref="Opcode.Of"/>. Both peers enroll the same domain+op so their opcodes agree. A domain
        /// or op value that overflows a byte throws (fail-fast). Prefer this when the game spans several
        /// subsystems so cross-subsystem collisions are structurally impossible.
        /// </summary>
        public void Enroll<T>(Enum domain, Enum op) where T : Message, new()
            => Enroll<T>(Opcode.Of(domain, op));

        /// <summary>
        /// Open a chainable registrar bound to a single <c>domain</c>, so the domain is written once and
        /// each message's op chains off it:
        /// <c>catalog.ForDomain(NetDomain.Wallet).Enroll&lt;SpendReq&gt;(WalletOp.Spend).Enroll&lt;SpendReply&gt;(WalletOp.SpendReply)</c>.
        /// A thin convenience over the banded <see cref="Enroll{T}(Enum, Enum)"/> overload — every op still
        /// folds through <see cref="Opcode.Of"/> against the captured domain.
        /// </summary>
        public DomainEnroller ForDomain(Enum domain) => new DomainEnroller(this, domain);

        /// <summary>
        /// Run several per-domain enrol blocks against this catalog in one call. Each registration is one
        /// domain's block (typically <c>c =&gt; c.ForDomain(...).Enroll&lt;...&gt;(...)...</c>); every block is
        /// invoked with <c>this</c> catalog. Lets a game keep its whole opcode sheet in one file and wire it
        /// with a single <c>catalog.EnrollAll(Wallet, Alliance, Reward)</c>.
        /// </summary>
        public void EnrollAll(params Action<MessageCatalog>[] registrations)
        {
            foreach (var register in registrations)
                register(this);
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

        /// <summary>
        /// A chainable registrar bound to one banded <c>domain</c>, returned by
        /// <see cref="MessageCatalog.ForDomain"/>. Holds the catalog plus the fixed domain so the caller
        /// writes the domain once and chains each message's op:
        /// <c>catalog.ForDomain(NetDomain.Wallet).Enroll&lt;SpendReq&gt;(WalletOp.Spend).Enroll&lt;SpendReply&gt;(WalletOp.SpendReply)</c>.
        /// A pass-through to <see cref="MessageCatalog.Enroll{T}(Enum, Enum)"/> — same duplicate/reserved
        /// guards, same <see cref="Opcode.Of"/> folding.
        /// </summary>
        public readonly struct DomainEnroller
        {
            readonly MessageCatalog _catalog;
            readonly Enum _domain;

            internal DomainEnroller(MessageCatalog catalog, Enum domain)
            {
                _catalog = catalog;
                _domain = domain;
            }

            /// <summary>Enroll one message type under this registrar's domain at <paramref name="op"/>, then
            /// return this registrar so further ops chain.</summary>
            public DomainEnroller Enroll<T>(Enum op) where T : Message, new()
            {
                _catalog.Enroll<T>(_domain, op);
                return this;
            }
        }
    }
}
