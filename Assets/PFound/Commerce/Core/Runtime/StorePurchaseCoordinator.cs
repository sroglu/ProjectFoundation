using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PFound.Commerce.Core
{
    /// <summary>
    /// Drives the real-money purchase flow end to end, engine-free: it initializes the store from the
    /// catalog's per-platform SKUs, resolves a product name to its SKU, runs the store transaction, and
    /// hands every resulting receipt — fresh, restored, or recovered-on-launch — to the
    /// <see cref="PurchasePipeline"/>, which grants ONLY after server verification and only once per
    /// order-id. Pending transactions raised by the store are reconciled the same way, so an interrupted
    /// purchase is never lost or double-granted. The current <see cref="StorePlatform"/> is injected by
    /// the Unity layer so SKU resolution stays testable off-device.
    /// </summary>
    public sealed class StorePurchaseCoordinator
    {
        readonly IStoreController _store;
        readonly PurchasePipeline _pipeline;
        readonly StorePlatform _platform;

        ProductCatalog _catalog;

        public StorePurchaseCoordinator(IStoreController store, PurchasePipeline pipeline, ProductCatalog catalog, StorePlatform platform)
        {
            _store = store;
            _pipeline = pipeline;
            _platform = platform;
            _catalog = catalog;
            _store.PendingTransaction += OnPendingTransaction;
        }

        public void UpdateCatalog(ProductCatalog catalog)
        {
            _catalog = catalog;
        }

        /// <summary>Register every real-money SKU on the current platform with the store.</summary>
        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            var products = new List<StoreProduct>();
            foreach (ProductDefinition product in _catalog.Products)
            {
                if (product.Cost.Kind != CostKind.RealMoney) continue;
                if (product.TryGetStoreId(_platform, out string storeId))
                {
                    products.Add(new StoreProduct(product.ProductName, storeId, product.Kind));
                }
            }
            return _store.InitializeAsync(products, cancellationToken);
        }

        /// <summary>
        /// Buy a real-money product: resolve its SKU, run the store transaction, then verify + grant the
        /// receipt. An unknown product or a product without a SKU on this platform fails cleanly.
        /// </summary>
        public async Task<PurchaseOutcome> PurchaseAsync(string productName, CancellationToken cancellationToken)
        {
            if (!_catalog.TryGet(productName, out ProductDefinition product))
            {
                return new PurchaseOutcome(PurchaseStatus.UnknownProduct, productName, "product '" + productName + "' is not in the catalog", null);
            }
            if (!product.TryGetStoreId(_platform, out string storeId))
            {
                return new PurchaseOutcome(PurchaseStatus.UnknownProduct, productName, "product '" + productName + "' has no SKU on " + _platform, null);
            }

            StorePurchaseAttempt attempt = await _store.PurchaseAsync(storeId, cancellationToken).ConfigureAwait(false);
            if (!attempt.Completed)
            {
                return new PurchaseOutcome(PurchaseStatus.VerificationFailed, productName, attempt.FailureReason, null);
            }

            return await VerifyGrantAndFinish(attempt.Receipt, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Restore non-consumables / subscriptions: verify + grant each restored receipt idempotently.</summary>
        public async Task<int> RestoreAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<PurchaseReceipt> receipts = await _store.RestoreAsync(cancellationToken).ConfigureAwait(false);
            int granted = 0;
            for (int i = 0; i < receipts.Count; i++)
            {
                PurchaseOutcome outcome = await VerifyGrantAndFinish(receipts[i], cancellationToken).ConfigureAwait(false);
                if (outcome.Status == PurchaseStatus.Granted) granted++;
            }
            return granted;
        }

        /// <summary>
        /// Verify + grant a receipt, then confirm it with the store — but only once it is safely granted
        /// (or was already granted). A verification failure leaves the transaction unfinished so it is
        /// retried, never silently consumed.
        /// </summary>
        async Task<PurchaseOutcome> VerifyGrantAndFinish(PurchaseReceipt receipt, CancellationToken cancellationToken)
        {
            PurchaseOutcome outcome = await _pipeline.ProcessReceiptAsync(receipt, cancellationToken).ConfigureAwait(false);
            if (outcome.Status == PurchaseStatus.Granted || outcome.Status == PurchaseStatus.AlreadyGranted)
            {
                _store.FinishTransaction(receipt);
            }
            return outcome;
        }

        void OnPendingTransaction(PurchaseReceipt receipt)
        {
            // Reconcile an interrupted transaction; verify + grant + finish is idempotent.
            _ = VerifyGrantAndFinish(receipt, CancellationToken.None);
        }
    }
}
