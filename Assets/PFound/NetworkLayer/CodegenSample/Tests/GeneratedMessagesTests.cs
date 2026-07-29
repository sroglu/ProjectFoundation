using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using PFound.NetworkLayer;
using PFound.NetworkLayer.CodegenSample;

namespace PFound.NetworkLayer.CodegenSample.Tests
{
    /// <summary>
    /// End-to-end proof that the source generator ran inside Unity: the generated nested types exist and
    /// are usable, the wire keys follow the explicit indices, the catalog enrols request + notify but not
    /// the reply, and a generated request DTO round-trips through the real MessagePack codec.
    /// </summary>
    public sealed class GeneratedMessagesTests
    {
        static ushort OpcodeOf<T>(MessageCatalog c) where T : Message, new() => c.OpcodeFor(typeof(T));

        [Test]
        public void RegisterAll_enrols_request_and_notify_but_not_reply()
        {
            var catalog = new MessageCatalog(new MessagePackBodyCodec());
            GeneratedMessages.RegisterAll(catalog);

            // request + notify carry a wire opcode…
            Assert.AreEqual((ushort)0x0701, OpcodeOf<Spend.RequestMessage>(catalog), "request opcode = Wallet<<8 | Spend");
            Assert.AreEqual((ushort)0x0703, OpcodeOf<BalanceChanged.NotifyMessage>(catalog), "notify opcode = Wallet<<8 | BalanceChanged");

            // …the reply does not (decoded by correlation), but is still poolable.
            Assert.Throws<KeyNotFoundException>(() => catalog.OpcodeFor(typeof(Spend.ReplyMessage)),
                "reply must NOT be enrolled by opcode");
            Assert.IsInstanceOf<Spend.ReplyMessage>(catalog.Take<Spend.ReplyMessage>(),
                "reply must still be registered for pooling");
        }

        [Test]
        public void Wire_keys_follow_explicit_indices_not_declaration_order()
        {
            // Source declared AccountId (index 1) BEFORE Amount (index 0); the keys must still match the indices.
            AssertKey(typeof(Spend.Req), nameof(Spend.Req.Amount), 0);
            AssertKey(typeof(Spend.Req), nameof(Spend.Req.AccountId), 1);
            AssertKey(typeof(Spend.Reply), nameof(Spend.Reply.NewBalance), 0);
            AssertKey(typeof(BalanceChanged.Data), nameof(BalanceChanged.Data.NewBalance), 0);
        }

        [Test]
        public void Request_dto_round_trips_through_messagepack()
        {
            var codec = new MessagePackBodyCodec();
            var sent = new Spend.RequestMessage { Content = new Spend.Req(50, 12345L) };

            byte[] bytes = codec.Pack(sent);
            var back = (Spend.RequestMessage)codec.Unpack(typeof(Spend.RequestMessage), new ArraySegment<byte>(bytes));

            Assert.AreEqual(sent.Content, back.Content, "struct value-equality survives the round-trip");
            Assert.AreEqual(50, back.Content.Amount);
            Assert.AreEqual(12345L, back.Content.AccountId);

            // zero-copy read accessor returns the same value without copying.
            ref readonly Spend.Req view = ref back.View;
            Assert.AreEqual(50, view.Amount);
        }

        [Test]
        public void Notify_dto_round_trips_through_messagepack()
        {
            var codec = new MessagePackBodyCodec();
            var sent = new BalanceChanged.NotifyMessage { Content = new BalanceChanged.Data(999L) };

            byte[] bytes = codec.Pack(sent);
            var back = (BalanceChanged.NotifyMessage)codec.Unpack(typeof(BalanceChanged.NotifyMessage), new ArraySegment<byte>(bytes));

            Assert.AreEqual(999L, back.Content.NewBalance);
        }

        static void AssertKey(Type dto, string fieldName, int expectedKey)
        {
            var field = dto.GetField(fieldName);
            var key = field.GetCustomAttribute<MessagePack.KeyAttribute>();
            Assert.NotNull(key, $"{dto.Name}.{fieldName} should carry [Key]");
            Assert.AreEqual(expectedKey, key.IntKey, $"{dto.Name}.{fieldName} key");
        }
    }
}
