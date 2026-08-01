using PFound.NetworkLayer;
using PFound.ServerOperation.Core;

namespace GameSpecific.Networking
{
    /// <summary>
    /// The bridge from a game <see cref="OpResult"/> to the framework's ready-made
    /// <see cref="ServerOperationResult{OpResult}"/>: the enum value IS the typed result code, so the failure
    /// presenter can name it back and resolve the localized toast. A flow reports success with <see cref="Ok"/>
    /// and a failure with <see cref="Fail"/>, mapping a wire <see cref="ReplyStatus"/> to the matching game
    /// outcome via <see cref="FromReplyStatus"/>.
    /// </summary>
    public static class OpResults
    {
        /// <summary>A successful result carrying the default (Invalid) <see cref="OpResult"/> code and no diagnostic.</summary>
        public static ServerOperationResult<OpResult> Ok()
            => ServerOperationResult<OpResult>.Success();

        /// <summary>A failed result carrying the <see cref="OpResult"/> as its typed code plus an optional log diagnostic.</summary>
        public static ServerOperationResult<OpResult> Fail(OpResult code, string diagnostic = null)
            => ServerOperationResult<OpResult>.Failure(code, diagnostic);

        /// <summary>The game outcome for a non-Ok server reply status — a refusal, a timeout, an unroutable send, else a fault.</summary>
        public static OpResult FromReplyStatus(ReplyStatus status)
            => status switch
            {
                ReplyStatus.Refused => OpResult.ServerRefused,
                ReplyStatus.Expired => OpResult.RequestExpired,
                ReplyStatus.Unroutable => OpResult.Unroutable,
                _ => OpResult.ServerFaulted,
            };
    }
}
