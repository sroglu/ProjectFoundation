using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PFound.Commerce.Core;
using Firebase.Functions;

namespace PFound.Commerce.Firebase
{
    /// <summary>
    /// Verifies a store receipt by calling a Firebase Cloud Function, which validates it against the
    /// Google Play Developer API / Apple App Store Server API server-side, writes the durable ledger, and
    /// returns verified/failed plus a canonical order-id. The client never validates receipts itself and
    /// never grants on the store SDK's word — only this backend's verified-success triggers a grant.
    ///
    /// Swapping to a self-host verifier is registering a different <see cref="IPurchaseVerifier"/>; the
    /// game's purchase call is unchanged. This assembly is gated behind <c>PFOUND_FIREBASE</c> and stays
    /// excluded until the Firebase SDK is present, so Commerce builds with the define off.
    /// </summary>
    public sealed class FirebasePurchaseVerifier : IPurchaseVerifier
    {
        readonly FirebaseFunctions _functions;
        readonly string _callableName;

        public FirebasePurchaseVerifier(FirebaseFunctions functions, string callableName = "verifyPurchase")
        {
            _functions = functions;
            _callableName = callableName;
        }

        public async Task<PurchaseVerification> VerifyAsync(PurchaseReceipt receipt, CancellationToken cancellationToken)
        {
            var request = new Dictionary<string, object>
            {
                { "productName", receipt.ProductName },
                { "storeProductId", receipt.StoreProductId },
                { "orderId", receipt.OrderId },
                { "platform", receipt.Platform.ToString() },
                { "payload", receipt.Payload },
                { "isRestored", receipt.IsRestored }
            };

            HttpsCallableReference callable = _functions.GetHttpsCallable(_callableName);
            HttpsCallableResult response = await callable.CallAsync(request).ConfigureAwait(false);

            if (!(response.Data is IDictionary<object, object> data))
            {
                return PurchaseVerification.Failure("verifier returned no data");
            }

            bool verified = data.TryGetValue("verified", out object verifiedValue) && verifiedValue is bool b && b;
            if (!verified)
            {
                string reason = data.TryGetValue("reason", out object reasonValue) ? reasonValue?.ToString() : "not verified";
                return PurchaseVerification.Failure(reason);
            }

            string canonicalOrderId = data.TryGetValue("canonicalOrderId", out object idValue) ? idValue?.ToString() : receipt.OrderId;
            return PurchaseVerification.Success(canonicalOrderId);
        }
    }
}
