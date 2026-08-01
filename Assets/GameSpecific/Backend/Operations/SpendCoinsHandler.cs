namespace GameSpecific.Backend.Operations
{
    using System.Threading;
    using System.Threading.Tasks;
    using PFound.Backend.Core;
    using PFound.NetworkLayer;
    using GameSpecific.Networking.Operations;
    using GameSpecific.Networking.Data;
    using GameSpecific.Backend.Repositories;

    public sealed class SpendCoinsHandler
        : IOperationHandler<SpendCoinsOperation.RequestMessage, SpendCoinsOperation.ReplyMessage>
    {
        readonly IWalletRepository _wallets;
        readonly ISessionStore _sessions;

        public SpendCoinsHandler(IWalletRepository wallets, ISessionStore sessions)
        {
            _wallets = wallets;
            _sessions = sessions;
        }

        public async Task<SpendCoinsOperation.ReplyMessage> HandleAsync(
            int peerId,
            SpendCoinsOperation.RequestMessage req,
            CancellationToken ct)
        {
            // RULE 3: Copy request values BEFORE await
            int amount = req.Content.Amount;

            // Resolve the player ID from the authenticated session
            if (!_sessions.TryGet(peerId, out var session))
            {
                return new SpendCoinsOperation.ReplyMessage
                {
                    Status = ReplyStatus.Refused,
                    Content = default
                };
            }
            long playerId = session.PlayerId;

            // RULE 3: All awaits after extraction
            var wallet = await _wallets.GetAsync(playerId, ct);

            // RULE 5: Check before reply, fail with status
            if (wallet.Coins < amount)
            {
                return new SpendCoinsOperation.ReplyMessage
                {
                    Status = ReplyStatus.Faulted,
                    Content = default
                };
            }

            wallet.Coins -= amount;
            await _wallets.SaveAsync(wallet, ct);

            // RULE 3: Build reply fresh, just before return
            return new SpendCoinsOperation.ReplyMessage
            {
                Status = ReplyStatus.Ok,
                Content = new SpendResult { NewBalance = wallet.Coins }
            };
            // RULE 2: Reply() called on pump thread in registry, not here ✅
        }
    }
}
