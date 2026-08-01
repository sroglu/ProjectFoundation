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
        readonly ISessionStore _sessions;

        public GetPlayerDataHandler(IPlayerRepository players, IWalletRepository wallets, ISessionStore sessions)
        {
            _players = players;
            _wallets = wallets;
            _sessions = sessions;
        }

        public async Task<GetPlayerDataOperation.ReplyMessage> HandleAsync(
            int peerId,
            GetPlayerDataOperation.RequestMessage req,
            CancellationToken ct)
        {
            // Resolve the player ID from the authenticated session
            if (!_sessions.TryGet(peerId, out var session))
            {
                return new GetPlayerDataOperation.ReplyMessage
                {
                    Status = ReplyStatus.Refused,
                    Content = default
                };
            }
            long playerId = session.PlayerId;

            // RULE 3: All awaits after extraction
            var profile = await _players.GetAsync(playerId, ct);
            var wallet = await _wallets.GetAsync(playerId, ct);

            // Build reply fresh, just before return
            return new GetPlayerDataOperation.ReplyMessage
            {
                Status = ReplyStatus.Ok,
                Content = new PlayerData
                {
                    PlayerId = new PlayerId { Value = playerId },
                    Level = profile.Level,
                    Name = profile.Name,
                    Coins = wallet.Coins
                }
            };
            // RULE 2: Reply() called on pump thread in registry, not here ✅
        }
    }
}
