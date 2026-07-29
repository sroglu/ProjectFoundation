using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PFound.Commerce.Core;
using PFound.RemoteGameConfig.Core;

namespace PFound.Commerce
{
    /// <summary>
    /// The game-facing commerce facade. It builds the cost and reward resolver registries from the game's
    /// player-state seams, reads the product catalog from a RemoteGameConfig section (rebuilding it on
    /// every config change), and exposes one purchase entry point that routes each product by its cost:
    /// real-money products go through the store + server-verify pipeline, everything else is settled
    /// locally. Rewards are granted only after a verified purchase; the ledger dedups on order-id so a
    /// grant happens exactly once. Analytics are emitted as plain events the game forwards to its
    /// analytics provider — Commerce takes no analytics-assembly dependency.
    ///
    /// The store transaction backend (Unity IAP) lives in a separate define-gated assembly; when it is
    /// present the game calls <see cref="AttachStore"/> to enable real-money purchases, restore, and live
    /// price display. Without it, the local-cost economy still works.
    /// </summary>
    public sealed class CommerceService
    {
        readonly RemoteGameConfigService _config;
        readonly CostResolverRegistry _costs;
        readonly RewardResolverRegistry _rewards;
        readonly CostCheckout _checkout;
        readonly PurchasePipeline _pipeline;
        readonly StorePlatform _platform;
        readonly Dictionary<string, Func<JsonValue, IReward>> _catalogRewardFactories = new Dictionary<string, Func<JsonValue, IReward>>();

        ProductCatalog _catalog = ProductCatalog.Empty;
        StorePurchaseCoordinator _storeCoordinator;
        IStoreController _store;
        bool _storeAttached;

        /// <summary>Analytics events (started / completed / failed) to forward to the game's provider.</summary>
        public event Action<CommerceAnalyticsEvent> AnalyticsEmitted;

        /// <summary>Raised once per verified, granted real-money purchase.</summary>
        public event Action<PurchaseLedgerEntry> PurchaseGranted;

        /// <summary>Raised after each reward is applied (currency / item / entitlement), for UI reactions.</summary>
        public event Action<IReward> RewardApplied
        {
            add => _rewards.RewardApplied += value;
            remove => _rewards.RewardApplied -= value;
        }

        public CommerceService(
            RemoteGameConfigService config,
            ICurrencyWallet wallet,
            IItemInventory inventory,
            IEntitlementStore entitlements,
            IRewardedAdPlayer ads,
            IPurchaseVerifier verifier,
            IPurchaseLedger ledger,
            ICommerceClock clock)
        {
            _config = config;
            _platform = StorePlatformResolver.Current;

            _costs = new CostResolverRegistry();
            _costs.Register(new FreeCostResolver());
            _costs.Register(new CurrencyCostResolver(wallet));
            _costs.Register(new RewardedAdCostResolver(ads));
            _costs.Register(new RealMoneyCostResolver());
            _costs.Register(new SelectableCostResolver(_costs));

            _rewards = new RewardResolverRegistry();
            _rewards.Register(new CurrencyRewardResolver(wallet));
            _rewards.Register(new InventoryItemRewardResolver(inventory));
            _rewards.Register(new EntitlementRewardResolver(entitlements));

            _pipeline = new PurchasePipeline(verifier, ledger, _rewards, entitlements, clock, _catalog);
            _pipeline.PurchaseGranted += entry => PurchaseGranted?.Invoke(entry);
            _pipeline.AnalyticsEmitted += e => AnalyticsEmitted?.Invoke(e);

            _checkout = new CostCheckout(_costs, _rewards, _catalog);
            _checkout.AnalyticsEmitted += e => AnalyticsEmitted?.Invoke(e);

            LoadCatalog();
            _config.Changed += OnConfigChanged;
        }

        /// <summary>The current parsed catalog (rebuilt on config changes).</summary>
        public ProductCatalog Catalog => _catalog;

        /// <summary>The global monetization switch from remote config.</summary>
        public bool AdsEnabled => _config.Current.Section(new CommerceCatalogSection()).AdsEnabled;

        /// <summary>
        /// Register a factory that parses a game-specific reward type from the catalog JSON. Applies to
        /// the next catalog load; register these before the first config fetch completes. Also register a
        /// matching <see cref="IRewardResolver"/> so the reward can be granted.
        /// </summary>
        public void RegisterCatalogRewardFactory(string rewardType, Func<JsonValue, IReward> factory)
        {
            _catalogRewardFactories[rewardType] = factory;
        }

        /// <summary>Register a game-specific reward resolver (extends the open reward set).</summary>
        public void RegisterRewardResolver(IRewardResolver resolver) => _rewards.Register(resolver);

        /// <summary>Register a game-specific cost resolver.</summary>
        public void RegisterCostResolver(ICostResolver resolver) => _costs.Register(resolver);

        /// <summary>
        /// Attach the store backend (Unity IAP) so real-money purchases, restore, and live pricing work.
        /// Called by the game once the IAP-gated wrapper is available. Initializes the store from the
        /// current catalog's SKUs.
        /// </summary>
        public Task AttachStore(IStoreController store, CancellationToken cancellationToken = default)
        {
            _store = store;
            _storeCoordinator = new StorePurchaseCoordinator(store, _pipeline, _catalog, _platform);
            _storeAttached = true;
            return _storeCoordinator.InitializeAsync(cancellationToken);
        }

        /// <summary>True when the product's cost can be paid right now (drives affordability UI).</summary>
        public bool CanAfford(string productName) => _checkout.CanAfford(productName);

        /// <summary>
        /// Buy a product. Local costs (currency / ad / free / selectable) settle immediately; a real-money
        /// product runs the store + server-verify flow and grants only on verified success. For a
        /// selectable cost, <paramref name="chosenOption"/> selects the branch.
        /// </summary>
        public async Task<PurchaseOutcome> BuyAsync(string productName, int chosenOption = -1, CancellationToken cancellationToken = default)
        {
            CheckoutResult local = _checkout.Buy(productName, chosenOption);
            switch (local.Status)
            {
                case CheckoutStatus.Purchased:
                    return new PurchaseOutcome(PurchaseStatus.Granted, productName, null, null);
                case CheckoutStatus.RequiresStorePurchase:
                    return await BuyFromStoreAsync(productName, cancellationToken).ConfigureAwait(false);
                case CheckoutStatus.UnknownProduct:
                    return new PurchaseOutcome(PurchaseStatus.UnknownProduct, productName, "unknown product", null);
                default:
                    return new PurchaseOutcome(PurchaseStatus.VerificationFailed, productName, local.Status.ToString(), null);
            }
        }

        async Task<PurchaseOutcome> BuyFromStoreAsync(string productName, CancellationToken cancellationToken)
        {
            if (!_storeAttached)
            {
                var failure = new PurchaseOutcome(PurchaseStatus.VerificationFailed, productName, "store backend not attached", null);
                AnalyticsEmitted?.Invoke(CommerceAnalyticsEvent.PurchaseFailed(productName, "store backend not attached"));
                return failure;
            }

            AnalyticsEmitted?.Invoke(CommerceAnalyticsEvent.PurchaseStarted(productName));
            return await _storeCoordinator.PurchaseAsync(productName, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Restore non-consumables / active subscriptions; grants each restored receipt idempotently.</summary>
        public Task<int> RestoreAsync(CancellationToken cancellationToken = default)
        {
            if (!_storeAttached) return Task.FromResult(0);
            return _storeCoordinator.RestoreAsync(cancellationToken);
        }

        /// <summary>
        /// Reconcile on-device entitlements against the server's authoritative owned set (refund/void).
        /// A revoked non-consumable is removed; a still-owned one stays granted.
        /// </summary>
        public void ReconcileEntitlements(IReadOnlyCollection<string> ownedFromServer) => _pipeline.ReconcileEntitlements(ownedFromServer);

        /// <summary>The live localized store price for a product, or a placeholder when the store is absent.</summary>
        public string PriceFor(string productName)
        {
            if (!_storeAttached) return string.Empty;
            if (!_catalog.TryGet(productName, out ProductDefinition product)) return string.Empty;
            if (!product.TryGetStoreId(_platform, out string storeId)) return string.Empty;
            return _store.LocalizedPrice(storeId);
        }

        void OnConfigChanged(GameConfig config) => LoadCatalog();

        void LoadCatalog()
        {
            string json = _config.Current.Section(new CommerceCatalogSection()).CatalogJson;
            if (!ProductCatalogReader.TryParse(json, _catalogRewardFactories, out ProductCatalog parsed))
            {
                // Malformed remote catalog: keep the last-good catalog, don't crash the economy.
                return;
            }

            _catalog = parsed;
            _pipeline.UpdateCatalog(_catalog);
            _checkout.UpdateCatalog(_catalog);
            if (_storeAttached) _storeCoordinator.UpdateCatalog(_catalog);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only: grant a product's rewards for free to simulate a purchase during development.
        /// Compiled only under UNITY_EDITOR — never present in a player build.
        /// </summary>
        public void EditorSimulateGrant(string productName)
        {
            if (!_catalog.TryGet(productName, out ProductDefinition product))
            {
                UnityEngine.Debug.LogWarning("[Commerce] Editor grant: unknown product '" + productName + "'.");
                return;
            }
            product.Rewards.ApplyThrough(_rewards);
        }
#endif
    }
}
