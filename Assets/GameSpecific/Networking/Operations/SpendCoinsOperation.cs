using System.Threading.Tasks;
using GameSpecific.Networking.Data;
using PFound.NetworkLayer;
using PFound.ServerOperation.Core;

namespace GameSpecific.Networking.Operations
{
    /// <summary>
    /// THE reference operation. Its server-authoritative lifecycle — client prediction (reject instantly with no
    /// request sent) → send → interpret → apply the authoritative state, with single-flight — lives in
    /// <see cref="SpendCoinsOperationFlow"/> below and is invoked through the uniform entry point:
    /// <c>await SpendCoinsOperation.Execute(amount);</c>. That <c>Execute(int amount)</c> is GENERATED (because a
    /// <see cref="SpendCoinsOperationFlow"/> exists): it takes ONLY the operation's own request parameters, news up
    /// the flow, and runs its lifecycle — so this operation class is EMPTY. The transport, outcome seams, run
    /// policy, and mutated game state are all ambient (resolved from <c>ServerOperationHost.Current</c> / the local
    /// wallet), never threaded through the call. The generated <c>Execute</c> returns a <see cref="Task"/>, so a
    /// caller either <c>await</c>s it or fires it and drops the outcome with <c>.Forget()</c>. The
    /// <c>[RemoteProcedure]</c> spec is the whole wire contract; the reply carries the <see cref="SpendResult"/>
    /// DTO (from <c>Data/</c>), NOT a bare <c>long</c>.
    /// </summary>
    [RemoteProcedure(NetDomain.Wallet, WalletOp.Spend, typeof(SpendRequest), typeof(SpendResult))]
    public partial class SpendCoinsOperation { }

    /// <summary>
    /// A trivial local wallet the flow predicts against and applies the server result to. It is ambient game state
    /// (<see cref="Current"/>) — a real game would reach its player/economy singleton here — so a flow reads it
    /// rather than having it injected, keeping <see cref="SpendCoinsOperation.Execute"/> down to just the amount.
    /// </summary>
    public sealed class PlayerWallet
    {
        public long Balance;

        /// <summary>The local player's wallet the spend flow predicts against and reconciles.</summary>
        public static PlayerWallet Current { get; set; } = new PlayerWallet();
    }

    /// <summary>
    /// The advanced lifecycle for one spend. Constructed with ONLY the amount — the base ctor resolves the
    /// transport + outcome seams + run policy from the ambient <c>ServerOperationHost.Current</c>, and the wallet
    /// it predicts against / applies to is the ambient <see cref="PlayerWallet.Current"/>. The four hooks below
    /// are the only code an author writes; they reuse the generated <c>SpendCoinsOperation.RequestMessage</c> /
    /// <c>SpendCoinsOperation.ReplyMessage</c> envelopes. Prefer the <see cref="SpendCoinsOperation.Execute"/>
    /// entry point at call sites; this class is what it news up.
    /// </summary>
    public sealed class SpendCoinsOperationFlow
        : ServerOperationFlow<SpendCoinsOperation.RequestMessage, SpendCoinsOperation.ReplyMessage, ServerOperationResult<OpResult>>
    {
        readonly SpendRequest _request;

        /// <summary>The authoritative reply DTO, available after a successful run.</summary>
        public SpendResult Result { get; private set; }

        public SpendCoinsOperationFlow(SpendRequest request) => _request = request;

        PlayerWallet Wallet => PlayerWallet.Current;

        protected override ServerOperationResult<OpResult> PreCheck()
        {
            if (_request.Amount <= 0)
                return OpResults.Fail(OpResult.AmountNotPositive, "amount must be positive");
            if (Wallet.Balance < _request.Amount)
                return OpResults.Fail(OpResult.InsufficientBalance, "insufficient balance");
            return OpResults.Ok();
        }

        protected override SpendCoinsOperation.RequestMessage BuildRequest()
            => new SpendCoinsOperation.RequestMessage { Content = _request };

        protected override ServerOperationResult<OpResult> Interpret(SpendCoinsOperation.ReplyMessage reply)
        {
            if (reply.Status != ReplyStatus.Ok)
                return OpResults.Fail(OpResults.FromReplyStatus(reply.Status), $"spend refused by server: {reply.Status}");
            Result = reply.Content;
            return OpResults.Ok();
        }

        protected override void ApplySuccess(ServerOperationResult<OpResult> result)
            => Wallet.Balance = Result.NewBalance;
    }
}
