using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PFound.Commerce.Core
{
    /// <summary>A product registered with the store, by its catalog name, per-platform SKU and behaviour.</summary>
    public readonly struct StoreProduct
    {
        public string ProductName { get; }
        public string StoreProductId { get; }
        public ProductKind Kind { get; }

        public StoreProduct(string productName, string storeProductId, ProductKind kind)
        {
            ProductName = productName;
            StoreProductId = storeProductId;
            Kind = kind;
        }
    }

    /// <summary>The store's answer to a purchase attempt: a receipt to verify, or a reason it did not complete.</summary>
    public readonly struct StorePurchaseAttempt
    {
        public bool Completed { get; }
        public PurchaseReceipt Receipt { get; }
        public string FailureReason { get; }

        StorePurchaseAttempt(bool completed, PurchaseReceipt receipt, string failureReason)
        {
            Completed = completed;
            Receipt = receipt;
            FailureReason = failureReason;
        }

        public static StorePurchaseAttempt Success(PurchaseReceipt receipt) => new StorePurchaseAttempt(true, receipt, null);
        public static StorePurchaseAttempt Failed(string reason) => new StorePurchaseAttempt(false, null, reason);
    }

    /// <summary>
    /// The store-transaction seam (implemented by the Unity IAP wrapper). It initializes the store from
    /// the catalog's SKUs, runs a purchase to a receipt, restores non-consumables / subscriptions, and
    /// surfaces receipts for transactions that completed at the store but were not finished on the client
    /// (interrupted-purchase recovery) via <see cref="PendingTransaction"/>. It never grants anything —
    /// granting is gated on server verification in <see cref="PurchasePipeline"/>. Keeping this interface
    /// in the engine-free Core lets the whole buy/restore/recover flow be unit-tested with a stub store.
    /// </summary>
    public interface IStoreController
    {
        bool IsInitialized { get; }

        Task InitializeAsync(IReadOnlyList<StoreProduct> products, CancellationToken cancellationToken);

        Task<StorePurchaseAttempt> PurchaseAsync(string storeProductId, CancellationToken cancellationToken);

        Task<IReadOnlyList<PurchaseReceipt>> RestoreAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Confirm a transaction with the store so it is not redelivered. Called ONLY after the receipt is
        /// verified and granted, so a purchase that is paid but not yet granted (crash / offline) is
        /// redelivered on next launch instead of being lost — the server-authoritative "finish after
        /// grant" handshake.
        /// </summary>
        void FinishTransaction(PurchaseReceipt receipt);

        /// <summary>The live localized store price for a SKU (empty until the store is initialized).</summary>
        string LocalizedPrice(string storeProductId);

        /// <summary>
        /// Raised for a store transaction that must be reconciled on the client (e.g. a purchase that
        /// completed while the app was closed). The subscriber verifies + grants it idempotently.
        /// </summary>
        event Action<PurchaseReceipt> PendingTransaction;
    }
}
