using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PFound.Commerce.Core;
using PFound.RemoteGameConfig.Core;

namespace PFound.Commerce.Tests
{
    internal sealed class FakeWallet : ICurrencyWallet
    {
        readonly Dictionary<string, long> _balances = new Dictionary<string, long>();

        public FakeWallet(string currencyId = null, long amount = 0)
        {
            if (currencyId != null) _balances[currencyId] = amount;
        }

        public long Balance(string currencyId) => _balances.TryGetValue(currencyId, out long b) ? b : 0;
        public bool CanSpend(string currencyId, long amount) => Balance(currencyId) >= amount;

        public bool TrySpend(string currencyId, long amount)
        {
            if (Balance(currencyId) < amount) return false;
            _balances[currencyId] = Balance(currencyId) - amount;
            return true;
        }

        public void Deposit(string currencyId, long amount) => _balances[currencyId] = Balance(currencyId) + amount;
    }

    internal sealed class FakeEntitlementStore : IEntitlementStore
    {
        readonly HashSet<string> _owned = new HashSet<string>();
        public bool Has(string entitlementId) => _owned.Contains(entitlementId);
        public void Grant(string entitlementId) => _owned.Add(entitlementId);
        public void Revoke(string entitlementId) => _owned.Remove(entitlementId);
    }

    internal sealed class FakeInventory : IItemInventory
    {
        readonly Dictionary<string, int> _items = new Dictionary<string, int>();
        public void Add(string itemId, int count) => _items[itemId] = Count(itemId) + count;
        public int Count(string itemId) => _items.TryGetValue(itemId, out int c) ? c : 0;
    }

    internal sealed class StubAdPlayer : IRewardedAdPlayer
    {
        readonly RewardedAdOutcome _outcome;
        public StubAdPlayer(RewardedAdOutcome outcome, bool available = true) { _outcome = outcome; IsAvailable = available; }
        public bool IsAvailable { get; }
        public RewardedAdOutcome Show() => _outcome;
    }

    internal sealed class FixedClock : ICommerceClock
    {
        public FixedClock(long now) { NowUnixSeconds = now; }
        public long NowUnixSeconds { get; }
    }

    internal sealed class StubVerifier : IPurchaseVerifier
    {
        readonly bool _verified;
        public int Calls { get; private set; }
        public StubVerifier(bool verified) { _verified = verified; }

        public Task<PurchaseVerification> VerifyAsync(PurchaseReceipt receipt, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(_verified
                ? PurchaseVerification.Success(receipt.OrderId)
                : PurchaseVerification.Failure("stub rejected"));
        }
    }

    internal sealed class StubStore : IStoreController
    {
        readonly Dictionary<string, string> _productNameBySku;
        readonly List<PurchaseReceipt> _restorable = new List<PurchaseReceipt>();

        public bool IsInitialized { get; private set; }
        public int FinishedCount { get; private set; }
        public event Action<PurchaseReceipt> PendingTransaction;

        public StubStore(Dictionary<string, string> productNameBySku) { _productNameBySku = productNameBySku; }

        public Task InitializeAsync(IReadOnlyList<StoreProduct> products, CancellationToken cancellationToken)
        {
            IsInitialized = true;
            return Task.CompletedTask;
        }

        public Task<StorePurchaseAttempt> PurchaseAsync(string storeProductId, CancellationToken cancellationToken)
        {
            string productName = _productNameBySku[storeProductId];
            var receipt = new PurchaseReceipt(productName, storeProductId, "order-" + storeProductId, StorePlatform.AppStore,
                "payload", 4.99m, "USD", false);
            return Task.FromResult(StorePurchaseAttempt.Success(receipt));
        }

        public void AddRestorable(PurchaseReceipt receipt) => _restorable.Add(receipt);
        public Task<IReadOnlyList<PurchaseReceipt>> RestoreAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<PurchaseReceipt>>(_restorable);

        public void FinishTransaction(PurchaseReceipt receipt) => FinishedCount++;
        public string LocalizedPrice(string storeProductId) => "$4.99";
        public void RaisePending(PurchaseReceipt receipt) => PendingTransaction?.Invoke(receipt);
    }

    internal sealed class StubInstallIdentity : IInstallIdentity
    {
        public string InstallId => "install-test";
    }

    /// <summary>An in-memory config source that hands back a preset document (the commerce catalog).</summary>
    internal sealed class StaticConfigSource : IGameConfigSource
    {
        readonly ConfigDocument _document;
        public StaticConfigSource(ConfigDocument document) { _document = document; }

        public Task<ConfigFetchResult> FetchAsync(ConfigFetchContext context, string knownVersion, bool forceRefresh, CancellationToken cancellationToken = default)
            => Task.FromResult(ConfigFetchResult.Fetched(_document, ServerOverrides.None, "v-test"));
    }
}
