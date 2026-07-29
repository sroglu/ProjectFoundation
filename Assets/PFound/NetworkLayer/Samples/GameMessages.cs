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

        static void Wallet(MessageCatalog c) => c.ForDomain(NetDomain.Wallet)
            .Enroll<SpendCoinsRequest>(WalletOp.Spend)
            .Enroll<SpendCoinsReply>(WalletOp.SpendReply);

        static void Alliance(MessageCatalog c) => c.ForDomain(NetDomain.Alliance)
            .Enroll<JoinAllianceRequest>(AllianceOp.Join)
            .Enroll<JoinAllianceReply>(AllianceOp.JoinReply);
    }
}
