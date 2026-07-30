using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PFound.Commerce.Core
{
    /// <summary>How a receipt-processing attempt ended.</summary>
    public enum PurchaseStatus
    {
        /// <summary>Verified and granted for the first time.</summary>
        Granted,

        /// <summary>The order-id was already granted; recognized and NOT granted again (idempotent replay).</summary>
        AlreadyGranted,

        /// <summary>Server verification did not confirm the receipt; nothing was granted.</summary>
        VerificationFailed,

        /// <summary>The product is not in the catalog; surfaced as a clean failure (config/programmer error).</summary>
        UnknownProduct,

        /// <summary>A consumable cannot be restored; a restore flow skips it without granting.</summary>
        NotRestorable
    }

    /// <summary>The result of processing one receipt.</summary>
    public readonly struct PurchaseOutcome
    {
        public PurchaseStatus Status { get; }
        public string ProductName { get; }
        public string FailureReason { get; }
        public PurchaseLedgerEntry Entry { get; }

        public PurchaseOutcome(PurchaseStatus status, string productName, string failureReason, PurchaseLedgerEntry entry)
        {
            Status = status;
            ProductName = productName;
            FailureReason = failureReason;
            Entry = entry;
        }

        public bool Granted => Status == PurchaseStatus.Granted;
    }

    /// <summary>
    /// The server-authoritative purchase flow — the security core. Given a completed store receipt it:
    /// (1) resolves the product from the catalog (unknown ⇒ clean failure); (2) skips already-granted
    /// order-ids so a replay/duplicate/retry grants exactly once; (3) verifies the receipt server-side and
    /// grants ONLY on verified-success — never on the store SDK's word; (4) records an append-only ledger
    /// entry and applies the reward set, raising the reward signal and a completed analytics event.
    /// A restore flow re-grants only non-consumables / subscriptions. Refund/void is reconciled by
    /// following the server's authoritative entitlement state. Nothing here trusts the client.
    /// </summary>
    public sealed class PurchasePipeline
    {
        readonly IPurchaseVerifier _verifier;
        readonly IPurchaseLedger _ledger;
        readonly RewardResolverRegistry _rewards;
        readonly IEntitlementStore _entitlements;
        readonly ICommerceClock _clock;

        ProductCatalog _catalog;

        /// <summary>Raised once per successfully granted purchase (drives the ledger UI / receipts).</summary>
        public event Action<PurchaseLedgerEntry> PurchaseGranted;

        /// <summary>Raised when a receipt fails to grant (unknown product / verification failure).</summary>
        public event Action<PurchaseOutcome> PurchaseFailed;

        /// <summary>Raised with the analytics event to forward to the game's analytics provider.</summary>
        public event Action<CommerceAnalyticsEvent> AnalyticsEmitted;

        public PurchasePipeline(
            IPurchaseVerifier verifier,
            IPurchaseLedger ledger,
            RewardResolverRegistry rewards,
            IEntitlementStore entitlements,
            ICommerceClock clock,
            ProductCatalog catalog)
        {
            _verifier = verifier;
            _ledger = ledger;
            _rewards = rewards;
            _entitlements = entitlements;
            _clock = clock;
            _catalog = catalog;
        }

        /// <summary>Swap in the newest catalog after a remote-config refresh.</summary>
        public void UpdateCatalog(ProductCatalog catalog)
        {
            _catalog = catalog;
        }

        /// <summary>
        /// Verify a completed store receipt and, only on success, grant it exactly once. Safe to call
        /// again with the same receipt (interrupted-purchase recovery, duplicate callbacks): a
        /// previously-granted order-id returns <see cref="PurchaseStatus.AlreadyGranted"/> without
        /// re-granting.
        /// </summary>
        public async Task<PurchaseOutcome> ProcessReceiptAsync(PurchaseReceipt receipt, CancellationToken cancellationToken)
        {
            if (!_catalog.TryGet(receipt.ProductName, out ProductDefinition product))
            {
                return Fail(PurchaseStatus.UnknownProduct, receipt.ProductName, "product '" + receipt.ProductName + "' is not in the catalog");
            }

            if (receipt.IsRestored && product.Kind == ProductKind.Consumable)
            {
                // Consumables are not restorable; a restore skips them (no grant, not a failure event).
                return new PurchaseOutcome(PurchaseStatus.NotRestorable, receipt.ProductName, null, null);
            }

            // First idempotency gate: the store order-id already granted.
            if (_ledger.Contains(receipt.OrderId))
            {
                return AlreadyGranted(receipt.ProductName, receipt.OrderId);
            }

            // Server-side verification is mandatory before any grant.
            PurchaseVerification verification = await _verifier.VerifyAsync(receipt, cancellationToken).ConfigureAwait(false);
            if (!verification.Verified)
            {
                var outcome = Fail(PurchaseStatus.VerificationFailed, receipt.ProductName, verification.FailureReason);
                return outcome;
            }

            // Second idempotency gate on the server's canonical order-id (the definitive dedup key).
            string orderId = string.IsNullOrEmpty(verification.CanonicalOrderId) ? receipt.OrderId : verification.CanonicalOrderId;
            if (_ledger.Contains(orderId))
            {
                return AlreadyGranted(receipt.ProductName, orderId);
            }

            return Grant(receipt, product, orderId);
        }

        PurchaseOutcome Grant(PurchaseReceipt receipt, ProductDefinition product, string orderId)
        {
            product.Rewards.ApplyThrough(_rewards);

            if (product.Kind != ProductKind.Consumable)
            {
                _entitlements.Grant(receipt.ProductName);
            }

            var entry = new PurchaseLedgerEntry(
                receipt.ProductName,
                receipt.StoreProductId,
                orderId,
                receipt.Price,
                receipt.CurrencyCode,
                _clock.NowUnixSeconds,
                receipt.IsRestored,
                product.Rewards.Describe());

            _ledger.Append(entry);

            PurchaseGranted?.Invoke(entry);
            AnalyticsEmitted?.Invoke(CommerceAnalyticsEvent.PurchaseCompleted(receipt.ProductName, receipt.Price, receipt.CurrencyCode));
            return new PurchaseOutcome(PurchaseStatus.Granted, receipt.ProductName, null, entry);
        }

        PurchaseOutcome AlreadyGranted(string productName, string orderId)
        {
            _ledger.TryGet(orderId, out PurchaseLedgerEntry entry);
            return new PurchaseOutcome(PurchaseStatus.AlreadyGranted, productName, null, entry);
        }

        PurchaseOutcome Fail(PurchaseStatus status, string productName, string reason)
        {
            var outcome = new PurchaseOutcome(status, productName, reason, null);
            PurchaseFailed?.Invoke(outcome);
            AnalyticsEmitted?.Invoke(CommerceAnalyticsEvent.PurchaseFailed(productName, reason));
            return outcome;
        }

        /// <summary>
        /// Apply the server's authoritative entitlement state after a refund/void reconcile: any owned
        /// non-consumable / subscription no longer present in <paramref name="ownedFromServer"/> is
        /// revoked on device. The server (driven by Google RTDN / Apple notifications) is the source of
        /// truth; the client follows it.
        /// </summary>
        public void ReconcileEntitlements(IReadOnlyCollection<string> ownedFromServer)
        {
            var owned = new HashSet<string>(ownedFromServer);
            for (int i = 0; i < _ledger.Entries.Count; i++)
            {
                PurchaseLedgerEntry entry = _ledger.Entries[i];
                if (!_catalog.TryGet(entry.ProductName, out ProductDefinition product)) continue;
                if (product.Kind == ProductKind.Consumable) continue;

                if (owned.Contains(entry.ProductName))
                {
                    _entitlements.Grant(entry.ProductName);
                }
                else
                {
                    _entitlements.Revoke(entry.ProductName);
                }
            }
        }
    }
}
