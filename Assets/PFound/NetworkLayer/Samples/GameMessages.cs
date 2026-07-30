using PFound.NetworkLayer.Samples.Wallet;
using PFound.NetworkLayer.Samples.Alliance;

namespace PFound.NetworkLayer.Samples
{
    /// <summary>
    /// The whole game's wire opcode sheet in one place — all domains registered here, collisions
    /// caught at a glance. Each domain's messages fold their per-domain op enum against
    /// <see cref="NetDomain"/> through <see cref="MessageCatalog.ForDomain"/>, so the band is written
    /// once and every op chains off it. A game boots its catalog with a single
    /// <c>GameMessages.RegisterAll(catalog)</c>.
    /// </summary>
    public static class GameMessages
    {
        public static void RegisterAll(MessageCatalog c) => c.EnrollAll(Wallet, Alliance);

        /// <summary>
        /// The production body codec for these sample DTOs: MessagePack's AOT source-generated formatters,
        /// composed from this assembly's <see cref="SampleNetworkResolver"/> (pushed explicitly — no reflection
        /// discovery) ahead of the builtin primitive/collection resolver. Build the catalog with this codec.
        /// </summary>
        public static MessagePackBodyCodec CreateCodec() => new MessagePackBodyCodec(SampleNetworkResolver.Instance);

        // Each RPC enrols as a request/reply PAIR: the request takes the opcode, the reply is registered
        // for pooling by type only (opcode-less — decoded by correlation, see MessageCatalog.RegisterReplyType).
        static void Wallet(MessageCatalog c) => c.ForDomain(NetDomain.Wallet)
            .Enroll<SpendCoinsRequest, SpendCoinsReply>(WalletOp.Spend);

        static void Alliance(MessageCatalog c) => c.ForDomain(NetDomain.Alliance)
            .Enroll<JoinAllianceRequest, JoinAllianceReply>(AllianceOp.Join);
    }
}
