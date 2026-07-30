using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using PFound.NetworkLayer;
using GameSpecific.Networking.Operations;
using GameSpecific.Networking.Data;

namespace GameSpecific.Networking.Tests
{
    /// <summary>
    /// End-to-end proof that both generators ran inside Unity against the real reference operations: the
    /// generated envelope types exist and carry the named DTOs, the catalog enrols each request but not its
    /// reply, the request DTO's wire keys follow the explicit indices, and request/reply DTOs round-trip
    /// through the real MessagePack codec (the envelope is a runtime-only carrier — only its DTO is serialized).
    /// </summary>
    public sealed class GeneratedMessagesTests
    {
        static MessageCatalog NewCatalog() => GameNetworkSetup.CreateCatalog();

        static ushort OpcodeOf<T>(MessageCatalog c) where T : Message, new() => c.OpcodeFor(typeof(T));

        [Test]
        public void RegisterAll_enrols_each_request_but_not_its_reply()
        {
            var catalog = NewCatalog();

            // requests carry a wire opcode = domain << 8 | op…
            Assert.AreEqual((ushort)0x0101, OpcodeOf<SpendCoins.RequestMessage>(catalog), "Wallet.Spend opcode");
            Assert.AreEqual((ushort)0x0201, OpcodeOf<JoinAlliance.RequestMessage>(catalog), "Alliance.Join opcode");
            Assert.AreEqual((ushort)0x0301, OpcodeOf<GetPlayerData.RequestMessage>(catalog), "Player.GetData opcode");

            // …replies do not (decoded by correlation), but are still poolable.
            Assert.Throws<KeyNotFoundException>(() => catalog.OpcodeFor(typeof(SpendCoins.ReplyMessage)),
                "reply must NOT be enrolled by opcode");
            Assert.IsInstanceOf<SpendCoins.ReplyMessage>(catalog.Take<SpendCoins.ReplyMessage>(),
                "reply must still be registered for pooling");
        }

        [Test]
        public void Request_dto_wire_keys_follow_explicit_indices()
        {
            AssertKey(typeof(SpendRequest), nameof(SpendRequest.Amount), 0);
            AssertKey(typeof(SpendResult), nameof(SpendResult.NewBalance), 0);
            AssertKey(typeof(JoinAllianceRequest), nameof(JoinAllianceRequest.AllianceId), 0);
            AssertKey(typeof(AllianceJoinResult), nameof(AllianceJoinResult.MemberCount), 0);
        }

        [Test]
        public void Request_dto_round_trips_through_messagepack()
        {
            var codec = GameNetworkSetup.CreateCodec();
            var sent = new SpendCoins.RequestMessage { Content = new SpendRequest { Amount = 50 } };

            byte[] bytes = codec.Pack(sent);
            var back = (SpendCoins.RequestMessage)codec.Unpack(typeof(SpendCoins.RequestMessage), new ArraySegment<byte>(bytes));

            Assert.AreEqual(sent.Content, back.Content, "DTO value-equality survives the round-trip");
            Assert.AreEqual(50, back.Content.Amount);

            // zero-copy read accessor returns the same value without copying.
            ref readonly SpendRequest view = ref back.View;
            Assert.AreEqual(50, view.Amount);
        }

        [Test]
        public void Reply_dto_round_trips_through_messagepack()
        {
            var codec = GameNetworkSetup.CreateCodec();

            // JoinAlliance reply IS the JoinResult DTO (never a bare int).
            var join = new JoinAlliance.ReplyMessage { Content = new AllianceJoinResult { MemberCount = 42 } };
            byte[] joinBytes = codec.Pack(join);
            var joinBack = (JoinAlliance.ReplyMessage)codec.Unpack(typeof(JoinAlliance.ReplyMessage), new ArraySegment<byte>(joinBytes));
            Assert.AreEqual(new AllianceJoinResult { MemberCount = 42 }, joinBack.Content, "JoinResult value-equality survives");
            Assert.AreEqual(42, joinBack.Content.MemberCount);

            // SpendCoins reply IS the SpendResult DTO (never a bare long).
            var spend = new SpendCoins.ReplyMessage { Content = new SpendResult { NewBalance = 990 } };
            byte[] spendBytes = codec.Pack(spend);
            var spendBack = (SpendCoins.ReplyMessage)codec.Unpack(typeof(SpendCoins.ReplyMessage), new ArraySegment<byte>(spendBytes));
            Assert.AreEqual(new SpendResult { NewBalance = 990 }, spendBack.Content, "SpendResult value-equality survives");
            Assert.AreEqual(990, spendBack.Content.NewBalance);
        }

        [Test]
        public void Shared_PlayerData_dto_round_trips_as_a_reply()
        {
            var codec = GameNetworkSetup.CreateCodec();
            var sent = new GetPlayerData.ReplyMessage { Content = new PlayerData { Level = 7, Coins = 1500, Name = "Ada" } };

            byte[] bytes = codec.Pack(sent);
            var back = (GetPlayerData.ReplyMessage)codec.Unpack(typeof(GetPlayerData.ReplyMessage), new ArraySegment<byte>(bytes));

            Assert.AreEqual(new PlayerData { Level = 7, Coins = 1500, Name = "Ada" }, back.Content, "PlayerData value-equality survives");
            Assert.AreEqual(7, back.Content.Level);
            Assert.AreEqual(1500, back.Content.Coins);
            Assert.AreEqual("Ada", back.Content.Name);
        }

        static void AssertKey(Type dto, string memberName, int expectedKey)
        {
            var member = (MemberInfo)dto.GetProperty(memberName) ?? dto.GetField(memberName);
            var key = member.GetCustomAttribute<MessagePack.KeyAttribute>();
            Assert.NotNull(key, $"{dto.Name}.{memberName} should carry [Key]");
            Assert.AreEqual(expectedKey, key.IntKey, $"{dto.Name}.{memberName} key");
        }
    }
}
