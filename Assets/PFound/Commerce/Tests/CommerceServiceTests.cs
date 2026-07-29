using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using PFound.Commerce.Core;
using PFound.RemoteGameConfig.Core;

namespace PFound.Commerce.Tests
{
    /// <summary>
    /// EditMode tests for the Unity glue: the catalog is read from a RemoteGameConfig section, the buy
    /// entry point routes each product by cost, real-money purchases are granted only after a verified
    /// store transaction and recorded in the ledger, and the editor-only test grant applies rewards.
    /// The store, verifier, and ad-play are stubs; the security logic itself is exhaustively covered by
    /// the engine-free Core mono suite.
    /// </summary>
    public sealed class CommerceServiceTests
    {
        // ---- catalog fixture served through RemoteGameConfig ----

        const string CatalogJson =
            "{ \"products\": {" +
            "  \"coins_small\": { \"category\": \"currency\", \"kind\": \"consumable\"," +
            "     \"cost\": { \"kind\": \"realmoney\" }," +
            "     \"sku\": { \"googleplay\": \"com.game.coins\", \"appstore\": \"com.game.coins\" }," +
            "     \"rewards\": [ { \"type\": \"currency\", \"id\": \"coins\", \"amount\": 100 } ] }," +
            "  \"remove_ads\": { \"category\": \"iap\", \"kind\": \"nonconsumable\"," +
            "     \"cost\": { \"kind\": \"realmoney\" }," +
            "     \"sku\": { \"googleplay\": \"com.game.remove_ads\", \"appstore\": \"com.game.remove_ads\" }," +
            "     \"rewards\": [ { \"type\": \"entitlement\", \"id\": \"no_ads\" } ] }," +
            "  \"revive\": { \"kind\": \"consumable\"," +
            "     \"cost\": { \"kind\": \"currency\", \"currencyId\": \"gems\", \"amount\": 50 }," +
            "     \"rewards\": [ { \"type\": \"item\", \"id\": \"revive_token\", \"count\": 1 } ] } } }";

        static RemoteGameConfigService BuildConfig()
        {
            string outer = "{ \"config\": { \"commerce\": { \"catalogJson\": " + JsonEscape(CatalogJson) + " } } }";
            ConfigDocumentReader.TryParse(outer, out ConfigDocument document);

            var sections = new GameConfigSection[] { new CommerceCatalogSection() };
            return new RemoteGameConfigService(sections, new StaticConfigSource(document), new StubInstallIdentity(),
                "1.0.0", "editor", "en-US", document);
        }

        static string JsonEscape(string raw)
        {
            var builder = new System.Text.StringBuilder(raw.Length + 2);
            builder.Append('"');
            foreach (char c in raw)
            {
                if (c == '"') builder.Append("\\\"");
                else if (c == '\\') builder.Append("\\\\");
                else builder.Append(c);
            }
            builder.Append('"');
            return builder.ToString();
        }

        static (CommerceService commerce, FakeWallet wallet, FakeInventory inventory, FakeEntitlementStore entitlements, InMemoryPurchaseLedger ledger, StubStore store, StubVerifier verifier)
            BuildCommerce(bool verified = true, long gems = 100)
        {
            RemoteGameConfigService config = BuildConfig();
            var wallet = new FakeWallet("gems", gems);
            var inventory = new FakeInventory();
            var entitlements = new FakeEntitlementStore();
            var ledger = new InMemoryPurchaseLedger();
            var verifier = new StubVerifier(verified);
            var ads = new StubAdPlayer(RewardedAdOutcome.Watched);

            var commerce = new CommerceService(config, wallet, inventory, entitlements, ads, verifier, ledger, new FixedClock(1_700_000_000));

            // Editor resolves App Store SKUs; both platforms share the SKU in this fixture.
            var store = new StubStore(new Dictionary<string, string>
            {
                { "com.game.coins", "coins_small" },
                { "com.game.remove_ads", "remove_ads" }
            });
            commerce.AttachStore(store).GetAwaiter().GetResult();

            return (commerce, wallet, inventory, entitlements, ledger, store, verifier);
        }

        [Test]
        public void Catalog_ReadFromRemoteConfigSection()
        {
            var (commerce, _, _, _, _, _, _) = BuildCommerce();
            Assert.AreEqual(3, commerce.Catalog.Count);
            Assert.IsTrue(commerce.Catalog.Contains("coins_small"));
            Assert.IsTrue(commerce.Catalog.Contains("revive"));
        }

        [Test]
        public void Buy_LocalCurrencyProduct_GrantsRewards()
        {
            var (commerce, wallet, inventory, _, _, _, _) = BuildCommerce();
            PurchaseOutcome outcome = commerce.BuyAsync("revive").GetAwaiter().GetResult();

            Assert.AreEqual(PurchaseStatus.Granted, outcome.Status);
            Assert.AreEqual(50, wallet.Balance("gems"));
            Assert.AreEqual(1, inventory.Count("revive_token"));
        }

        [Test]
        public void Buy_RealMoney_VerifiesGrantsAndRecordsLedgerAndRevenue()
        {
            var (commerce, wallet, _, _, ledger, store, _) = BuildCommerce(verified: true);

            string revenueProduct = null;
            commerce.AnalyticsEmitted += e =>
            {
                if (e.Name == CommerceAnalyticsEvent.PurchaseCompletedName && e.Properties.TryGetValue("product", out object prod))
                {
                    revenueProduct = prod.ToString();
                }
            };

            PurchaseOutcome outcome = commerce.BuyAsync("coins_small").GetAwaiter().GetResult();

            Assert.AreEqual(PurchaseStatus.Granted, outcome.Status);
            Assert.AreEqual(100, wallet.Balance("coins"));
            Assert.AreEqual(1, ledger.Entries.Count);
            Assert.AreEqual("coins_small", revenueProduct, "revenue analytics emitted for the product");
            Assert.AreEqual(1, store.FinishedCount, "transaction finished after grant");
        }

        [Test]
        public void Buy_RealMoney_FailedVerification_DoesNotGrant()
        {
            var (commerce, wallet, _, _, ledger, store, _) = BuildCommerce(verified: false);
            PurchaseOutcome outcome = commerce.BuyAsync("coins_small").GetAwaiter().GetResult();

            Assert.AreEqual(PurchaseStatus.VerificationFailed, outcome.Status);
            Assert.AreEqual(0, wallet.Balance("coins"));
            Assert.AreEqual(0, ledger.Entries.Count);
            Assert.AreEqual(0, store.FinishedCount, "unverified transaction not finished");
        }

        [Test]
        public void Buy_RealMoney_DuplicateOrderId_GrantsOnce()
        {
            var (commerce, wallet, _, _, ledger, _, verifier) = BuildCommerce(verified: true);
            // Same store order-id both times (StubStore derives order-id from the SKU) -> idempotent.
            commerce.BuyAsync("coins_small").GetAwaiter().GetResult();
            PurchaseOutcome second = commerce.BuyAsync("coins_small").GetAwaiter().GetResult();

            Assert.AreEqual(PurchaseStatus.AlreadyGranted, second.Status);
            Assert.AreEqual(100, wallet.Balance("coins"), "granted exactly once");
            Assert.AreEqual(1, ledger.Entries.Count);
            Assert.AreEqual(1, verifier.Calls, "second attempt short-circuits before re-verifying");
        }

        [Test]
        public void InterruptedTransaction_ReconciledToOneGrant()
        {
            var (commerce, wallet, _, _, ledger, store, _) = BuildCommerce(verified: true);
            var pending = new PurchaseReceipt("coins_small", "com.game.coins", "pending-order", StorePlatform.AppStore, "payload", 4.99m, "USD", false);

            store.RaisePending(pending);

            Assert.AreEqual(100, wallet.Balance("coins"), "interrupted purchase granted once");
            Assert.AreEqual(1, ledger.Entries.Count);
        }

        [Test]
        public void Restore_NonConsumableGranted_ConsumableNot()
        {
            var (commerce, _, _, entitlements, ledger, store, _) = BuildCommerce(verified: true);
            store.AddRestorable(new PurchaseReceipt("remove_ads", "com.game.remove_ads", "restore-1", StorePlatform.AppStore, "payload", 0m, "USD", true));
            store.AddRestorable(new PurchaseReceipt("coins_small", "com.game.coins", "restore-2", StorePlatform.AppStore, "payload", 0m, "USD", true));

            int granted = commerce.RestoreAsync().GetAwaiter().GetResult();

            Assert.AreEqual(1, granted, "only the non-consumable is restored");
            Assert.IsTrue(entitlements.Has("remove_ads"));
            Assert.IsTrue(entitlements.Has("no_ads"));
            Assert.AreEqual(1, ledger.Entries.Count, "consumable restore adds no ledger entry");
        }

        [Test]
        public void Refund_ReconcileRevokesEntitlement()
        {
            var (commerce, _, _, entitlements, _, _, _) = BuildCommerce(verified: true);
            commerce.BuyAsync("remove_ads").GetAwaiter().GetResult();
            Assert.IsTrue(entitlements.Has("remove_ads"));

            commerce.ReconcileEntitlements(new string[0]);
            Assert.IsFalse(entitlements.Has("remove_ads"), "refund revokes entitlement");
        }

        [Test]
        public void Price_ReadsLocalizedStorePrice()
        {
            var (commerce, _, _, _, _, _, _) = BuildCommerce();
            Assert.AreEqual("$4.99", commerce.PriceFor("coins_small"));
        }

#if UNITY_EDITOR
        [Test]
        public void EditorSimulateGrant_AppliesRewardsFree()
        {
            var (commerce, wallet, _, _, ledger, _, _) = BuildCommerce();
            commerce.EditorSimulateGrant("coins_small");

            Assert.AreEqual(100, wallet.Balance("coins"), "editor grant applies rewards");
            Assert.AreEqual(0, ledger.Entries.Count, "editor grant does not touch the ledger");
        }
#endif
    }
}
