using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PFound.Commerce.Core;
using UnityEngine;
using UnityEngine.Purchasing;
using CoreStorePlatform = PFound.Commerce.Core.StorePlatform;
using UnityStoreController = UnityEngine.Purchasing.IStoreController;

namespace PFound.Commerce.UnityIap
{
    /// <summary>
    /// The Unity IAP implementation of the Commerce store seam. It initializes the store from the
    /// catalog's per-platform SKUs, runs a purchase to a platform receipt, restores non-consumables /
    /// subscriptions, and reports the localized store price. Transactions are held <b>pending</b> until
    /// the Commerce pipeline has verified and granted them, then confirmed via
    /// <see cref="FinishTransaction"/> — so a paid-but-ungranted purchase (crash / offline) is redelivered
    /// to <see cref="PendingTransaction"/> on the next launch and reconciled, never lost or double-granted.
    ///
    /// This whole assembly is gated behind <c>PFOUND_UNITY_IAP</c> and stays excluded until the
    /// <c>com.unity.purchasing</c> package is installed and the define is set, so the rest of Commerce
    /// compiles without the package. It never grants anything: granting is server-verified in the Core.
    /// </summary>
    public sealed class UnityIapStoreController : PFound.Commerce.Core.IStoreController, IStoreListener
    {
        readonly CoreStorePlatform _platform;
        readonly Dictionary<string, string> _productNameBySku = new Dictionary<string, string>();
        readonly Dictionary<string, TaskCompletionSource<StorePurchaseAttempt>> _pendingPurchases = new Dictionary<string, TaskCompletionSource<StorePurchaseAttempt>>();

        UnityStoreController _controller;
        IExtensionProvider _extensions;
        TaskCompletionSource<bool> _initialization;
        bool _ready;

        public UnityIapStoreController(CoreStorePlatform platform)
        {
            _platform = platform;
        }

        public bool IsInitialized => _ready;

        public event Action<PurchaseReceipt> PendingTransaction;

        // ---- initialization ----

        public Task InitializeAsync(IReadOnlyList<StoreProduct> products, CancellationToken cancellationToken)
        {
            _initialization = new TaskCompletionSource<bool>();

            ConfigurationBuilder builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            for (int i = 0; i < products.Count; i++)
            {
                StoreProduct product = products[i];
                _productNameBySku[product.StoreProductId] = product.ProductName;
                builder.AddProduct(product.StoreProductId, ToUnityProductType(product.Kind));
            }

            UnityPurchasing.Initialize(this, builder);
            return _initialization.Task;
        }

        public void OnInitialized(UnityStoreController controller, IExtensionProvider extensions)
        {
            _controller = controller;
            _extensions = extensions;
            _ready = true;
            _initialization.TrySetResult(true);
        }

        public void OnInitializeFailed(InitializationFailureReason error) => OnInitializeFailed(error, null);

        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            _initialization.TrySetException(new InvalidOperationException("Unity IAP init failed: " + error + " " + message));
        }

        // ---- purchase ----

        public Task<StorePurchaseAttempt> PurchaseAsync(string storeProductId, CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<StorePurchaseAttempt>();
            _pendingPurchases[storeProductId] = completion;

            Product product = _controller.products.WithID(storeProductId);
            if (product == null || !product.availableToPurchase)
            {
                _pendingPurchases.Remove(storeProductId);
                completion.TrySetResult(StorePurchaseAttempt.Failed("product '" + storeProductId + "' unavailable"));
                return completion.Task;
            }

            _controller.InitiatePurchase(product);
            return completion.Task;
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            PurchaseReceipt receipt = BuildReceipt(args.purchasedProduct, isRestored: false);

            if (_pendingPurchases.TryGetValue(args.purchasedProduct.definition.storeSpecificId, out TaskCompletionSource<StorePurchaseAttempt> completion))
            {
                _pendingPurchases.Remove(args.purchasedProduct.definition.storeSpecificId);
                completion.TrySetResult(StorePurchaseAttempt.Success(receipt));
            }
            else
            {
                // A transaction with no in-flight request: interrupted / restored / server-pushed. Reconcile it.
                PendingTransaction?.Invoke(receipt);
            }

            // Keep the transaction pending until the Core verifies + grants, then FinishTransaction confirms it.
            return PurchaseProcessingResult.Pending;
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            if (_pendingPurchases.TryGetValue(product.definition.storeSpecificId, out TaskCompletionSource<StorePurchaseAttempt> completion))
            {
                _pendingPurchases.Remove(product.definition.storeSpecificId);
                completion.TrySetResult(StorePurchaseAttempt.Failed(failureReason.ToString()));
            }
        }

        public void FinishTransaction(PurchaseReceipt receipt)
        {
            Product product = _controller.products.WithID(receipt.StoreProductId);
            if (product != null) _controller.ConfirmPendingPurchase(product);
        }

        // ---- restore ----

        public Task<IReadOnlyList<PurchaseReceipt>> RestoreAsync(CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<IReadOnlyList<PurchaseReceipt>>();

            // Restored transactions arrive through ProcessPurchase -> PendingTransaction (reconciled by the
            // coordinator). This call triggers the platform restore and reports only completion/failure.
            if (_platform == CoreStorePlatform.AppStore)
            {
                _extensions.GetExtension<IAppleExtensions>().RestoreTransactions(result =>
                    completion.TrySetResult(new List<PurchaseReceipt>()));
            }
            else
            {
                _extensions.GetExtension<IGooglePlayStoreExtensions>().RestoreTransactions(result =>
                    completion.TrySetResult(new List<PurchaseReceipt>()));
            }

            return completion.Task;
        }

        // ---- price ----

        public string LocalizedPrice(string storeProductId)
        {
            if (!_ready) return string.Empty;
            Product product = _controller.products.WithID(storeProductId);
            return product == null ? string.Empty : product.metadata.localizedPriceString;
        }

        // ---- mapping ----

        PurchaseReceipt BuildReceipt(Product product, bool isRestored)
        {
            _productNameBySku.TryGetValue(product.definition.storeSpecificId, out string productName);
            return new PurchaseReceipt(
                productName,
                product.definition.storeSpecificId,
                product.transactionID,
                _platform,
                product.receipt,
                product.metadata.localizedPrice,
                product.metadata.isoCurrencyCode,
                isRestored);
        }

        static ProductType ToUnityProductType(ProductKind kind)
        {
            switch (kind)
            {
                case ProductKind.NonConsumable: return ProductType.NonConsumable;
                case ProductKind.Subscription: return ProductType.Subscription;
                default: return ProductType.Consumable;
            }
        }
    }
}
