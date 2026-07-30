using System.Collections.Generic;
using System.Threading;

namespace PFound.Commerce.Core.Tests
{
    /// <summary>
    /// Standalone mono/csc runner for the engine-free Commerce core: cost resolvers (currency / free /
    /// rewarded-ad / selectable), reward resolvers + the reward signal, catalog per-platform SKU
    /// resolution + JSON parse + unknown-product handling, and the security core — verify-before-grant,
    /// idempotent grant, interrupted-purchase recovery, restore rules, and refund/void reconciliation.
    /// </summary>
    internal static class Program
    {
        static int Main()
        {
            CostResolvers_CurrencyAndFree();
            CostResolvers_RewardedAdGrantsOnlyOnWatched();
            CostResolvers_SelectableResolvesChosenBranch();
            RewardResolvers_ApplyRewardSetAndSignal();
            RewardResolvers_NoAdsFlipsFlag();
            Catalog_PerPlatformSkuResolves();
            Catalog_UnknownProductFailsCleanly();
            Catalog_ParsesFromJson();
            Pipeline_VerifiedSuccessGrantsOnce();
            Pipeline_FailedVerificationDoesNotGrant();
            Pipeline_IdempotentOnDuplicateOrderId();
            Pipeline_CanonicalOrderIdDedups();
            Pipeline_InterruptedPurchaseReconcilesToOneGrant();
            Pipeline_RestoreNonConsumableButNotConsumable();
            Pipeline_RefundRevokesEntitlement();
            Ledger_AppendOnlyEntryShape();
            Checkout_CurrencyPurchaseGrantsRewards();
            Checkout_RealMoneyRoutesToStore();
            Checkout_SelectableChoosesBranch();
            Coordinator_StoreBuyVerifiesAndGrants();
            Coordinator_FailedVerificationDoesNotFinishTransaction();
            Coordinator_InitializeRegistersRealMoneySkus();
            Coordinator_RestoreGrantsRestoredReceipts();
            Coordinator_PendingTransactionReconciles();

            return TestKit.Summary("Commerce.Core");
        }

        // ---- helpers ----

        static ProductCatalog SampleCatalog()
        {
            var coins = new ProductDefinition(
                "coins_small", "currency", ProductKind.Consumable,
                new RealMoneyCost("coins_small"),
                new RewardSet(new IReward[] { new CurrencyReward("coins", 100) }),
                new Dictionary<StorePlatform, string>
                {
                    { StorePlatform.GooglePlay, "com.game.coins_small" },
                    { StorePlatform.AppStore, "com.game.ios.coins_small" }
                });

            var removeAds = new ProductDefinition(
                "remove_ads", "iap", ProductKind.NonConsumable,
                new RealMoneyCost("remove_ads"),
                new RewardSet(new IReward[] { new EntitlementReward("no_ads") }),
                new Dictionary<StorePlatform, string>
                {
                    { StorePlatform.GooglePlay, "com.game.remove_ads" },
                    { StorePlatform.AppStore, "com.game.ios.remove_ads" }
                });

            var revive = new ProductDefinition(
                "revive", "gameplay", ProductKind.Consumable,
                new CurrencyCost("gems", 50),
                new RewardSet(new IReward[] { new InventoryItemReward("revive_token", 1) }),
                new Dictionary<StorePlatform, string>());

            return new ProductCatalog(new[] { coins, removeAds, revive });
        }

        static RewardResolverRegistry Rewards(FakeWallet wallet, FakeInventory inventory, FakeEntitlementStore entitlements)
        {
            var rewards = new RewardResolverRegistry();
            rewards.Register(new CurrencyRewardResolver(wallet));
            rewards.Register(new InventoryItemRewardResolver(inventory));
            rewards.Register(new EntitlementRewardResolver(entitlements));
            return rewards;
        }

        static PurchaseReceipt Receipt(string productName, string orderId, bool restored = false)
        {
            return new PurchaseReceipt(productName, "sku_" + productName, orderId, StorePlatform.GooglePlay,
                "payload", 4.99m, "USD", restored);
        }

        // ---- cost resolvers ----

        static void CostResolvers_CurrencyAndFree()
        {
            var wallet = new FakeWallet("gems", 100);
            var registry = new CostResolverRegistry();
            registry.Register(new FreeCostResolver());
            registry.Register(new CurrencyCostResolver(wallet));

            TestKit.Check(registry.CanPay(FreeCost.Instance), "free is always payable");
            TestKit.Check(registry.Pay(FreeCost.Instance).Succeeded, "free pays");

            var cost = new CurrencyCost("gems", 50);
            TestKit.Check(registry.CanPay(cost), "currency CanPay with sufficient balance");
            TestKit.Check(registry.Pay(cost).Succeeded, "currency Pay succeeds");
            TestKit.Check(wallet.Balance("gems") == 50, "currency spent exactly");

            var tooMuch = new CurrencyCost("gems", 999);
            TestKit.Check(!registry.CanPay(tooMuch), "currency CanPay false when short");
            CostPayment failed = registry.Pay(tooMuch);
            TestKit.Check(failed.Status == CostPaymentStatus.InsufficientFunds, "currency Pay fails when short");
            TestKit.Check(wallet.Balance("gems") == 50, "failed pay does not spend");
        }

        static void CostResolvers_RewardedAdGrantsOnlyOnWatched()
        {
            var watched = new CostResolverRegistry();
            watched.Register(new RewardedAdCostResolver(new StubAdPlayer(RewardedAdOutcome.Watched)));
            TestKit.Check(watched.Pay(RewardedAdCost.Instance).Succeeded, "ad cost pays on watched");

            var skipped = new CostResolverRegistry();
            skipped.Register(new RewardedAdCostResolver(new StubAdPlayer(RewardedAdOutcome.Skipped)));
            TestKit.Check(skipped.Pay(RewardedAdCost.Instance).Status == CostPaymentStatus.AdNotWatched, "ad cost fails on skip");

            var unavailable = new CostResolverRegistry();
            unavailable.Register(new RewardedAdCostResolver(new StubAdPlayer(RewardedAdOutcome.Failed, available: false)));
            TestKit.Check(!unavailable.CanPay(RewardedAdCost.Instance), "ad cost not payable when no ad available");
        }

        static void CostResolvers_SelectableResolvesChosenBranch()
        {
            var wallet = new FakeWallet("gems", 100);
            var registry = new CostResolverRegistry();
            registry.Register(new CurrencyCostResolver(wallet));
            registry.Register(new RewardedAdCostResolver(new StubAdPlayer(RewardedAdOutcome.Watched)));
            registry.Register(new SelectableCostResolver(registry));

            var selectable = new SelectableCost(new ICost[]
            {
                new CurrencyCost("gems", 40),
                RewardedAdCost.Instance
            });

            TestKit.Check(registry.CanPay(selectable), "selectable payable when any option is");
            TestKit.Check(registry.Pay(selectable).Status == CostPaymentStatus.NoChoice, "selectable not paid unresolved");

            // Resolve the chosen branch then pay it as a normal cost.
            TestKit.Check(registry.Pay(selectable.Option(0)).Succeeded, "chosen currency branch pays");
            TestKit.Check(wallet.Balance("gems") == 60, "chosen branch spent");
            TestKit.Check(registry.Pay(selectable.Option(1)).Succeeded, "chosen ad branch pays on watched");
        }

        // ---- reward resolvers ----

        static void RewardResolvers_ApplyRewardSetAndSignal()
        {
            var wallet = new FakeWallet();
            var inventory = new FakeInventory();
            var entitlements = new FakeEntitlementStore();
            RewardResolverRegistry rewards = Rewards(wallet, inventory, entitlements);

            int signalled = 0;
            rewards.RewardApplied += r => signalled++;

            var set = new RewardSet(new IReward[]
            {
                new CurrencyReward("coins", 100),
                new InventoryItemReward("sword", 2)
            });
            set.ApplyThrough(rewards);

            TestKit.Check(wallet.Balance("coins") == 100, "currency reward applied");
            TestKit.Check(inventory.Count("sword") == 2, "item reward applied");
            TestKit.Check(signalled == 2, "reward signal fired per reward");
        }

        static void RewardResolvers_NoAdsFlipsFlag()
        {
            var entitlements = new FakeEntitlementStore();
            RewardResolverRegistry rewards = Rewards(new FakeWallet(), new FakeInventory(), entitlements);

            TestKit.Check(!entitlements.Has("no_ads"), "no_ads off before grant");
            new RewardSet(new IReward[] { new EntitlementReward("no_ads") }).ApplyThrough(rewards);
            TestKit.Check(entitlements.Has("no_ads"), "no_ads flag flipped by reward");
        }

        // ---- catalog ----

        static void Catalog_PerPlatformSkuResolves()
        {
            ProductCatalog catalog = SampleCatalog();
            catalog.TryGet("coins_small", out ProductDefinition coins);

            coins.TryGetStoreId(StorePlatform.GooglePlay, out string android);
            coins.TryGetStoreId(StorePlatform.AppStore, out string ios);
            TestKit.Check(android == "com.game.coins_small", "android sku resolves");
            TestKit.Check(ios == "com.game.ios.coins_small", "ios sku resolves");

            TestKit.Check(catalog.TryGetByStoreId(StorePlatform.AppStore, "com.game.ios.coins_small", out ProductDefinition byId)
                && byId.ProductName == "coins_small", "reverse sku lookup resolves product");
        }

        static void Catalog_UnknownProductFailsCleanly()
        {
            ProductCatalog catalog = SampleCatalog();
            TestKit.Check(!catalog.TryGet("nope", out _), "unknown product not found");
            TestKit.Check(!catalog.Contains("nope"), "unknown product Contains false");
        }

        static void Catalog_ParsesFromJson()
        {
            const string json =
                "{ \"products\": {" +
                "  \"coins_small\": { \"category\": \"currency\", \"kind\": \"consumable\"," +
                "     \"cost\": { \"kind\": \"realmoney\" }," +
                "     \"sku\": { \"googleplay\": \"com.g.coins\", \"appstore\": \"com.g.ios.coins\" }," +
                "     \"rewards\": [ { \"type\": \"currency\", \"id\": \"coins\", \"amount\": 100 } ] }," +
                "  \"double\": { \"cost\": { \"kind\": \"rewardedad\" }," +
                "     \"rewards\": [ { \"type\": \"currency\", \"id\": \"coins\", \"amount\": 20 } ] }," +
                "  \"starter\": { \"kind\": \"nonconsumable\"," +
                "     \"cost\": { \"kind\": \"selectable\", \"options\": [ { \"kind\": \"currency\", \"currencyId\": \"gems\", \"amount\": 100 }, { \"kind\": \"rewardedad\" } ] }," +
                "     \"rewards\": [ { \"type\": \"entitlement\", \"id\": \"starter_pack\" }, { \"type\": \"item\", \"id\": \"potion\", \"count\": 3 } ] } } }";

            TestKit.Check(ProductCatalogReader.TryParse(json, out ProductCatalog catalog), "catalog json parses");
            TestKit.Check(catalog.Count == 3, "three products parsed");

            catalog.TryGet("coins_small", out ProductDefinition coins);
            TestKit.Check(coins.Kind == ProductKind.Consumable, "kind parsed");
            TestKit.Check(coins.Cost.Kind == CostKind.RealMoney, "realmoney cost parsed");
            coins.TryGetStoreId(StorePlatform.GooglePlay, out string sku);
            TestKit.Check(sku == "com.g.coins", "sku parsed");
            TestKit.Check(coins.Rewards.Count == 1, "reward parsed");

            catalog.TryGet("double", out ProductDefinition dbl);
            TestKit.Check(dbl.Cost.Kind == CostKind.RewardedAd, "rewardedad cost parsed");

            catalog.TryGet("starter", out ProductDefinition starter);
            TestKit.Check(starter.Cost.Kind == CostKind.Selectable, "selectable cost parsed");
            TestKit.Check(((SelectableCost)starter.Cost).OptionCount == 2, "selectable options parsed");
            TestKit.Check(starter.Rewards.Count == 2, "multi-reward parsed");

            TestKit.Check(!ProductCatalogReader.TryParse("{ not json", out _), "malformed json rejected (no throw)");
        }

        // ---- security core: pipeline ----

        static (PurchasePipeline pipeline, FakeWallet wallet, FakeEntitlementStore entitlements, InMemoryPurchaseLedger ledger, StubVerifier verifier) BuildPipeline(bool verified, string canonicalOrderId = null)
        {
            var wallet = new FakeWallet();
            var inventory = new FakeInventory();
            var entitlements = new FakeEntitlementStore();
            var ledger = new InMemoryPurchaseLedger();
            var verifier = new StubVerifier(verified, canonicalOrderId);
            var pipeline = new PurchasePipeline(verifier, ledger, Rewards(wallet, inventory, entitlements), entitlements,
                new FixedClock(1_700_000_000), SampleCatalog());
            return (pipeline, wallet, entitlements, ledger, verifier);
        }

        static void Pipeline_VerifiedSuccessGrantsOnce()
        {
            var (pipeline, wallet, _, ledger, _) = BuildPipeline(verified: true);
            int granted = 0;
            pipeline.PurchaseGranted += _ => granted++;
            CommerceAnalyticsEvent lastEvent = default;
            pipeline.AnalyticsEmitted += e => lastEvent = e;

            PurchaseOutcome outcome = pipeline.ProcessReceiptAsync(Receipt("coins_small", "order-1"), CancellationToken.None).GetAwaiter().GetResult();

            TestKit.Check(outcome.Status == PurchaseStatus.Granted, "verified success grants");
            TestKit.Check(wallet.Balance("coins") == 100, "reward granted on verified success");
            TestKit.Check(ledger.Entries.Count == 1, "ledger has one entry");
            TestKit.Check(granted == 1, "granted signal fired once");
            TestKit.Check(lastEvent.Name == CommerceAnalyticsEvent.PurchaseCompletedName, "revenue analytics emitted");
        }

        static void Pipeline_FailedVerificationDoesNotGrant()
        {
            var (pipeline, wallet, _, ledger, _) = BuildPipeline(verified: false);
            PurchaseOutcome failEvent = default;
            pipeline.PurchaseFailed += e => failEvent = e;

            PurchaseOutcome outcome = pipeline.ProcessReceiptAsync(Receipt("coins_small", "order-2"), CancellationToken.None).GetAwaiter().GetResult();

            TestKit.Check(outcome.Status == PurchaseStatus.VerificationFailed, "failed verification does not grant");
            TestKit.Check(wallet.Balance("coins") == 0, "no reward on failed verification");
            TestKit.Check(ledger.Entries.Count == 0, "no ledger entry on failed verification");
            TestKit.Check(failEvent.Status == PurchaseStatus.VerificationFailed, "failed signal fired");
        }

        static void Pipeline_IdempotentOnDuplicateOrderId()
        {
            var (pipeline, wallet, _, ledger, verifier) = BuildPipeline(verified: true);

            PurchaseReceipt receipt = Receipt("coins_small", "order-3");
            PurchaseOutcome first = pipeline.ProcessReceiptAsync(receipt, CancellationToken.None).GetAwaiter().GetResult();
            PurchaseOutcome second = pipeline.ProcessReceiptAsync(receipt, CancellationToken.None).GetAwaiter().GetResult();

            TestKit.Check(first.Status == PurchaseStatus.Granted, "first grants");
            TestKit.Check(second.Status == PurchaseStatus.AlreadyGranted, "duplicate order-id recognized");
            TestKit.Check(wallet.Balance("coins") == 100, "reward granted exactly once");
            TestKit.Check(ledger.Entries.Count == 1, "ledger has exactly one entry");
            TestKit.Check(verifier.Calls == 1, "second attempt short-circuits before re-verifying");
        }

        static void Pipeline_CanonicalOrderIdDedups()
        {
            // Two different store order-ids that the server normalizes to the same canonical id grant once.
            var (pipeline, wallet, _, ledger, _) = BuildPipeline(verified: true, canonicalOrderId: "canon-1");

            pipeline.ProcessReceiptAsync(Receipt("coins_small", "store-a"), CancellationToken.None).GetAwaiter().GetResult();
            PurchaseOutcome second = pipeline.ProcessReceiptAsync(Receipt("coins_small", "store-b"), CancellationToken.None).GetAwaiter().GetResult();

            TestKit.Check(second.Status == PurchaseStatus.AlreadyGranted, "canonical order-id dedups across store ids");
            TestKit.Check(wallet.Balance("coins") == 100, "canonical dedup grants once");
            TestKit.Check(ledger.Entries.Count == 1, "canonical dedup one ledger entry");
        }

        static void Pipeline_InterruptedPurchaseReconcilesToOneGrant()
        {
            // Simulate a completed store purchase whose grant did not finish, replayed on next launch.
            var (pipeline, wallet, _, ledger, _) = BuildPipeline(verified: true);
            PurchaseReceipt pending = Receipt("coins_small", "order-interrupted");

            // First launch: crash after store success but the same receipt is re-processed on next launch.
            pipeline.ProcessReceiptAsync(pending, CancellationToken.None).GetAwaiter().GetResult();
            pipeline.ProcessReceiptAsync(pending, CancellationToken.None).GetAwaiter().GetResult();

            TestKit.Check(wallet.Balance("coins") == 100, "interrupted purchase grants exactly once");
            TestKit.Check(ledger.Entries.Count == 1, "interrupted purchase one ledger entry");
        }

        static void Pipeline_RestoreNonConsumableButNotConsumable()
        {
            var (pipeline, _, entitlements, ledger, _) = BuildPipeline(verified: true);

            PurchaseOutcome nonConsumable = pipeline.ProcessReceiptAsync(Receipt("remove_ads", "restore-1", restored: true), CancellationToken.None).GetAwaiter().GetResult();
            TestKit.Check(nonConsumable.Status == PurchaseStatus.Granted, "non-consumable restored");
            TestKit.Check(entitlements.Has("remove_ads"), "restored non-consumable owned");
            TestKit.Check(entitlements.Has("no_ads"), "restored non-consumable reward applied");

            PurchaseOutcome consumable = pipeline.ProcessReceiptAsync(Receipt("coins_small", "restore-2", restored: true), CancellationToken.None).GetAwaiter().GetResult();
            TestKit.Check(consumable.Status == PurchaseStatus.NotRestorable, "consumable not restored");
            TestKit.Check(ledger.Entries.Count == 1, "consumable restore adds no ledger entry");
        }

        static void Pipeline_RefundRevokesEntitlement()
        {
            var (pipeline, _, entitlements, _, _) = BuildPipeline(verified: true);
            pipeline.ProcessReceiptAsync(Receipt("remove_ads", "buy-1"), CancellationToken.None).GetAwaiter().GetResult();
            TestKit.Check(entitlements.Has("remove_ads"), "entitlement owned after purchase");

            // Server reports the entitlement is no longer owned (refund/void); client reconciles.
            pipeline.ReconcileEntitlements(new string[0]);
            TestKit.Check(!entitlements.Has("remove_ads"), "refund revokes entitlement on reconcile");

            // Server still owns it -> reconcile keeps it granted.
            pipeline.ReconcileEntitlements(new[] { "remove_ads" });
            TestKit.Check(entitlements.Has("remove_ads"), "server-owned entitlement stays granted");
        }

        static void Ledger_AppendOnlyEntryShape()
        {
            var (pipeline, _, _, ledger, _) = BuildPipeline(verified: true);
            pipeline.ProcessReceiptAsync(Receipt("coins_small", "order-ledger"), CancellationToken.None).GetAwaiter().GetResult();

            PurchaseLedgerEntry entry = ledger.Entries[0];
            TestKit.Check(entry.ProductName == "coins_small", "entry carries product");
            TestKit.Check(entry.StoreProductId == "sku_coins_small", "entry carries sku");
            TestKit.Check(entry.OrderId == "order-ledger", "entry carries order-id");
            TestKit.Check(entry.Price == 4.99m, "entry carries price");
            TestKit.Check(entry.CurrencyCode == "USD", "entry carries currency");
            TestKit.Check(entry.PurchasedAtUnixSeconds == 1_700_000_000, "entry carries timestamp");
            TestKit.Check(entry.GrantedRewards.Count == 1, "entry carries granted rewards");

            bool threw = false;
            try { ledger.Append(entry); }
            catch (System.ArgumentException) { threw = true; }
            TestKit.Check(threw, "duplicate append rejected (append-only)");
        }

        // ---- non-store checkout ----

        static CostCheckout BuildCheckout(FakeWallet wallet, FakeInventory inventory, FakeEntitlementStore entitlements, IRewardedAdPlayer ads)
        {
            var costs = new CostResolverRegistry();
            costs.Register(new FreeCostResolver());
            costs.Register(new CurrencyCostResolver(wallet));
            costs.Register(new RewardedAdCostResolver(ads));
            costs.Register(new RealMoneyCostResolver());
            costs.Register(new SelectableCostResolver(costs));
            return new CostCheckout(costs, Rewards(wallet, inventory, entitlements), SampleCatalog());
        }

        static void Checkout_CurrencyPurchaseGrantsRewards()
        {
            var wallet = new FakeWallet("gems", 100);
            var inventory = new FakeInventory();
            CostCheckout checkout = BuildCheckout(wallet, inventory, new FakeEntitlementStore(), new StubAdPlayer(RewardedAdOutcome.Watched));

            TestKit.Check(checkout.CanAfford("revive"), "revive affordable");
            CheckoutResult result = checkout.Buy("revive");
            TestKit.Check(result.Purchased, "currency purchase succeeds");
            TestKit.Check(wallet.Balance("gems") == 50, "currency spent on checkout");
            TestKit.Check(inventory.Count("revive_token") == 1, "reward granted on checkout");

            CheckoutResult unknown = checkout.Buy("nope");
            TestKit.Check(unknown.Status == CheckoutStatus.UnknownProduct, "unknown product fails cleanly");
        }

        static void Checkout_RealMoneyRoutesToStore()
        {
            CostCheckout checkout = BuildCheckout(new FakeWallet(), new FakeInventory(), new FakeEntitlementStore(), new StubAdPlayer(RewardedAdOutcome.Watched));
            CheckoutResult result = checkout.Buy("coins_small");
            TestKit.Check(result.Status == CheckoutStatus.RequiresStorePurchase, "real-money routes to store pipeline");
        }

        static void Checkout_SelectableChoosesBranch()
        {
            // A selectable product bought locally: option 0 = currency, option 1 = ad.
            var wallet = new FakeWallet("gems", 100);
            var inventory = new FakeInventory();
            var entitlements = new FakeEntitlementStore();
            var costs = new CostResolverRegistry();
            costs.Register(new CurrencyCostResolver(wallet));
            costs.Register(new RewardedAdCostResolver(new StubAdPlayer(RewardedAdOutcome.Watched)));
            costs.Register(new SelectableCostResolver(costs));

            var product = new ProductDefinition(
                "starter_choice", "bundle", ProductKind.Consumable,
                new SelectableCost(new ICost[] { new CurrencyCost("gems", 40), RewardedAdCost.Instance }),
                new RewardSet(new IReward[] { new CurrencyReward("coins", 500) }),
                new Dictionary<StorePlatform, string>());
            var catalog = new ProductCatalog(new[] { product });

            var checkout = new CostCheckout(costs, Rewards(wallet, inventory, entitlements), catalog);

            CheckoutResult noChoice = checkout.Buy("starter_choice");
            TestKit.Check(noChoice.Status == CheckoutStatus.NoChoice, "selectable without a choice fails");

            CheckoutResult currencyBranch = checkout.Buy("starter_choice", chosenOption: 0);
            TestKit.Check(currencyBranch.Purchased, "selectable currency branch purchases");
            TestKit.Check(wallet.Balance("gems") == 60, "selectable currency branch spent");
            TestKit.Check(wallet.Balance("coins") == 500, "selectable reward granted");

            CheckoutResult adBranch = checkout.Buy("starter_choice", chosenOption: 1);
            TestKit.Check(adBranch.Purchased, "selectable ad branch purchases on watched");
        }

        // ---- store coordinator (full real-money flow) ----

        static (StorePurchaseCoordinator coordinator, StubStore store, FakeWallet wallet, FakeEntitlementStore entitlements, InMemoryPurchaseLedger ledger) BuildCoordinator()
        {
            var (pipeline, wallet, entitlements, ledger, _) = BuildPipeline(verified: true);
            var store = new StubStore(new Dictionary<string, string>
            {
                { "com.game.coins_small", "coins_small" },
                { "com.game.remove_ads", "remove_ads" }
            });
            var coordinator = new StorePurchaseCoordinator(store, pipeline, SampleCatalog(), StorePlatform.GooglePlay);
            return (coordinator, store, wallet, entitlements, ledger);
        }

        static void Coordinator_StoreBuyVerifiesAndGrants()
        {
            var (coordinator, store, wallet, _, ledger) = BuildCoordinator();
            PurchaseOutcome outcome = coordinator.PurchaseAsync("coins_small", CancellationToken.None).GetAwaiter().GetResult();

            TestKit.Check(outcome.Status == PurchaseStatus.Granted, "store buy verifies and grants");
            TestKit.Check(wallet.Balance("coins") == 100, "store buy grants reward");
            TestKit.Check(ledger.Entries.Count == 1, "store buy records ledger entry");
            TestKit.Check(store.FinishedCount == 1, "transaction finished only after grant");
            TestKit.Check(store.LastFinishedOrderId == "order-com.game.coins_small", "correct transaction finished");
        }

        static void Coordinator_FailedVerificationDoesNotFinishTransaction()
        {
            var (pipeline, _, _, _, _) = BuildPipeline(verified: false);
            var store = new StubStore(new Dictionary<string, string> { { "com.game.coins_small", "coins_small" } });
            var coordinator = new StorePurchaseCoordinator(store, pipeline, SampleCatalog(), StorePlatform.GooglePlay);

            PurchaseOutcome outcome = coordinator.PurchaseAsync("coins_small", CancellationToken.None).GetAwaiter().GetResult();
            TestKit.Check(outcome.Status == PurchaseStatus.VerificationFailed, "unverified store buy not granted");
            TestKit.Check(store.FinishedCount == 0, "unverified transaction left unfinished for retry");
        }

        static void Coordinator_InitializeRegistersRealMoneySkus()
        {
            var (coordinator, store, _, _, _) = BuildCoordinator();
            coordinator.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();

            TestKit.Check(store.IsInitialized, "store initialized");
            // coins_small + remove_ads are real-money on GooglePlay; revive is a currency cost (no SKU).
            TestKit.Check(store.Registered.Count == 2, "only real-money SKUs registered");
        }

        static void Coordinator_RestoreGrantsRestoredReceipts()
        {
            var (coordinator, store, _, entitlements, _) = BuildCoordinator();
            store.AddRestorable(new PurchaseReceipt("remove_ads", "com.game.remove_ads", "restore-order", StorePlatform.GooglePlay, "payload", 0m, "USD", true));

            int granted = coordinator.RestoreAsync(CancellationToken.None).GetAwaiter().GetResult();
            TestKit.Check(granted == 1, "restore grants the restored non-consumable");
            TestKit.Check(entitlements.Has("remove_ads"), "restored entitlement owned");
        }

        static void Coordinator_PendingTransactionReconciles()
        {
            var (_, store, wallet, _, ledger) = BuildCoordinator();
            // A transaction that completed at the store while the app was closed, replayed on launch.
            store.RaisePending(new PurchaseReceipt("coins_small", "com.game.coins_small", "pending-order", StorePlatform.GooglePlay, "payload", 4.99m, "USD", false));

            TestKit.Check(wallet.Balance("coins") == 100, "pending transaction reconciled to a grant");
            TestKit.Check(ledger.Entries.Count == 1, "pending transaction recorded once");
        }
    }
}
