using System.Threading;
using System.Threading.Tasks;

namespace PFound.Commerce.Core
{
    /// <summary>
    /// The verifier's authoritative answer for a receipt. Only <see cref="Verified"/> == true may trigger
    /// a grant; the client never grants on the store SDK's word alone. The canonical order-id is the
    /// server's normalized transaction id, used as the definitive idempotency key when present.
    /// </summary>
    public readonly struct PurchaseVerification
    {
        public bool Verified { get; }
        public string CanonicalOrderId { get; }
        public string FailureReason { get; }

        PurchaseVerification(bool verified, string canonicalOrderId, string failureReason)
        {
            Verified = verified;
            CanonicalOrderId = canonicalOrderId;
            FailureReason = failureReason;
        }

        public static PurchaseVerification Success(string canonicalOrderId) => new PurchaseVerification(true, canonicalOrderId, null);
        public static PurchaseVerification Failure(string reason) => new PurchaseVerification(false, null, reason);
    }

    /// <summary>
    /// The pluggable server-verification backend. It sends the receipt to a server that validates it
    /// against Google Play / Apple and answers verified/failed plus the canonical order-id. Swapping the
    /// backend (Firebase Cloud Function, self-host endpoint) is swapping the registered implementation;
    /// game code that calls purchase never names a backend. The suggested name is kept from the spec.
    /// </summary>
    public interface IPurchaseVerifier
    {
        Task<PurchaseVerification> VerifyAsync(PurchaseReceipt receipt, CancellationToken cancellationToken);
    }
}
