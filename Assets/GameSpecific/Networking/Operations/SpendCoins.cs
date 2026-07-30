using GameSpecific.Networking.Data;
using PFound.NetworkLayer;
using PFound.ServerOperation.Core;

namespace GameSpecific.Networking.Operations
{
    /// <summary>
    /// THE reference operation, shown BOTH ways so the trade-off is visible in one file.
    ///
    /// <para><b>Primary path (uniform, what most calls use):</b>
    /// <c>var result = await SpendCoins.CallAsync(amount);</c> — the generator emits <c>CallAsync</c> from the
    /// <c>[RemoteProcedure]</c> spec below; it builds the request, sends it through the ambient client, awaits,
    /// and returns the <see cref="SpendResult"/> DTO. Same shape as every other operation.</para>
    ///
    /// <para><b>Advanced path (opt-in, this file's <see cref="SpendCoinsOperation"/>):</b> the
    /// server-authoritative lifecycle — client prediction (reject instantly with no request sent) → send →
    /// interpret → apply the authoritative state, with single-flight. Reach for a <c>ServerOperation</c> only
    /// when a call MUTATES local state that must be predicted then reconciled; a plain <c>CallAsync</c> covers
    /// everything else. The lifecycle reuses the SAME generated messages (<c>SpendCoins.RequestMessage</c> /
    /// <c>SpendCoins.ReplyMessage</c>), so opting in costs no extra wire declaration.</para>
    ///
    /// The <c>[RemoteProcedure]</c> spec is the whole wire contract either way. The reply carries the
    /// <see cref="SpendResult"/> DTO (from <c>Data/</c>), NOT a bare <c>long</c>: a reply is always a DTO.
    /// </summary>
    [RemoteProcedure(NetDomain.Wallet, WalletOp.Spend, typeof(SpendRequest), typeof(SpendResult))]
    public partial class SpendCoins { }

    // ---------------------------------------------------------------------------------------------------
    // ADVANCED (opt-in): the server-authoritative lifecycle for the SAME operation. Everything below is only
    // needed for client-prediction + apply-authoritative-state + single-flight mutations. A plain
    // `await SpendCoins.CallAsync(amount)` is the documented default and needs none of it.
    // ---------------------------------------------------------------------------------------------------

    /// <summary>A trivial local wallet the operation predicts against and applies the server result to.</summary>
    public sealed class PlayerWallet
    {
        public long Balance;
    }

    /// <summary>
    /// The advanced lifecycle for one spend. Constructed per run with the amount and the wallet it acts on;
    /// the four hooks below are the only code an operation author writes, and they reuse the generated
    /// <c>SpendCoins.RequestMessage</c> / <c>SpendCoins.ReplyMessage</c> envelopes.
    /// </summary>
    public sealed class SpendCoinsOperation
        : ServerOperation<SpendCoins.RequestMessage, SpendCoins.ReplyMessage, ServerOperationResult>
    {
        readonly int _amount;
        readonly PlayerWallet _wallet;
        long _confirmedBalance; // stashed by Interpret, applied by ApplySuccess

        public SpendCoinsOperation(
            ServerOperationContext<SpendCoins.RequestMessage, SpendCoins.ReplyMessage, ServerOperationResult> context,
            int amount,
            PlayerWallet wallet)
            : base(context)
        {
            _amount = amount;
            _wallet = wallet;
        }

        // Client prediction: reject instantly (no request sent) when the amount is off or funds are short.
        protected override ServerOperationResult PreCheck()
        {
            if (_amount <= 0)
                return ServerOperationResult.Failure(-1, "amount must be positive");
            if (_wallet.Balance < _amount)
                return ServerOperationResult.Failure(-2, "insufficient balance");
            return ServerOperationResult.Success();
        }

        // Build the typed request envelope from this run's parameters (the same one CallAsync builds).
        protected override SpendCoins.RequestMessage BuildRequest()
            => new SpendCoins.RequestMessage { Content = new SpendRequest { Amount = _amount } };

        // Map the server's reply to a result — the server, not the client, decides success. The balance is
        // read off the reply DTO (reply.Content is the SpendResult).
        protected override ServerOperationResult Interpret(SpendCoins.ReplyMessage reply)
        {
            if (reply.Status != ReplyStatus.Ok)
                return ServerOperationResult.Failure((int)reply.Status, $"spend refused by server: {reply.Status}");
            _confirmedBalance = reply.Content.NewBalance;
            return ServerOperationResult.Success();
        }

        // Apply the authoritative state synchronously (no awaits) once the server confirmed the spend.
        protected override void ApplySuccess(ServerOperationResult result)
            => _wallet.Balance = _confirmedBalance;
    }
}
