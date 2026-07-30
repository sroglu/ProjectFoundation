using MessagePack;
using MessagePack.Resolvers;
using NUnit.Framework;
using GameSpecific.Networking;
using GameSpecific.Networking.Data;

namespace GameSpecific.Networking.Tests
{
    /// <summary>
    /// Proves the DTO authoring surface: a DTO author writes only <c>[MessagePackObject]</c> + <c>[Key]</c>
    /// init-only properties; the PFound generator supplies value equality + <c>ToString</c>, and MessagePack's
    /// own source generator supplies the wire formatter (reached through this assembly's generated
    /// <see cref="GameNetworkResolver"/>). Serialization is AOT-safe — no runtime IL, no reflection discovery.
    /// </summary>
    public sealed class DtoGeneratorTests
    {
        // The exact resolver composition the production codec uses: this assembly's generated formatters first,
        // then the builtin primitive/collection resolver. No dynamic/contractless resolver.
        static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions.Standard
            .WithResolver(CompositeResolver.Create(GameNetworkResolver.Instance, BuiltinResolver.Instance));

        [Test]
        public void Generator_supplies_equality_and_tostring()
        {
            var a = new PlayerData { Level = 7, Coins = 1500, Name = "Ada" };
            var b = new PlayerData { Level = 7, Coins = 1500, Name = "Ada" };
            var c = new PlayerData { Level = 7, Coins = 1501, Name = "Ada" };

            Assert.AreEqual(a, b, "generated value equality");
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode(), "equal values, equal hash");
            Assert.AreNotEqual(a, c, "differing member breaks equality");
            Assert.AreEqual("PlayerData(Level=7, Coins=1500, Name=Ada)", a.ToString(), "generated ToString");
            Assert.AreEqual(7, a.Level);
            Assert.AreEqual(1500, a.Coins);
            Assert.AreEqual("Ada", a.Name);
        }

        [Test]
        public void Generated_resolver_owns_the_dto()
        {
            Assert.IsNotNull(GameNetworkResolver.Instance.GetFormatter<PlayerData>(),
                "the assembly's generated resolver supplies the PlayerData formatter");
        }

        [Test]
        public void Standalone_dto_round_trips_through_the_generated_formatter()
        {
            var sent = new PlayerData { Level = 42, Coins = 9_000_000_000L, Name = "Grace" };

            byte[] bytes = MessagePackSerializer.Serialize(sent, Options);
            var back = MessagePackSerializer.Deserialize<PlayerData>(bytes, Options);

            Assert.AreEqual(sent, back, "DTO value-equality survives the round-trip");
            Assert.AreEqual(42, back.Level);
            Assert.AreEqual(9_000_000_000L, back.Coins);
            Assert.AreEqual("Grace", back.Name);
        }
    }
}
