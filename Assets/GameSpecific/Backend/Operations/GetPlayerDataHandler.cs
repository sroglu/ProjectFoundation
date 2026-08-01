namespace GameSpecific.Backend.Operations
{
    using System.Threading;
    using System.Threading.Tasks;
    using PFound.Backend.Core;
    using PFound.NetworkLayer;
    using GameSpecific.Networking.Operations;
    using GameSpecific.Networking.Data;
    using GameSpecific.Backend.Repositories;

    public sealed class GetPlayerDataHandler
        : IOperationHandler<GetPlayerDataOperation.RequestMessage, GetPlayerDataOperation.ReplyMessage>
    {
        readonly IPlayerRepository _players;
        readonly IWalletRepository _wallets;

        public GetPlayerDataHandler(IPlayerRepository players, IWalletRepository wallets)
        {
            _players = players;
            _wallets = wallets;
        }

        public async Task<GetPlayerDataOperation.ReplyMessage> HandleAsync(
            int peerId,
            GetPlayerDataOperation.RequestMessage req,
            CancellationToken ct)
        {
            // RULE 3: Copy request values BEFORE await
            var playerId = req.Content.Value;

            // RULE 3: All awaits after extraction
            var profile = await _players.GetAsync(playerId, ct);
            var wallet = await _wallets.GetAsync(playerId, ct);

            // Build reply fresh, just before return
            return new GetPlayerDataOperation.ReplyMessage
            {
                Status = ReplyStatus.Ok,
                Content = new PlayerData
                {
                    PlayerId = req.Content,
                    Level = profile.Level,
                    Name = profile.Name,
                    Coins = wallet.Coins
                }
            };
            // RULE 2: Reply() called on pump thread in registry, not here ✅
        }
    }
}
