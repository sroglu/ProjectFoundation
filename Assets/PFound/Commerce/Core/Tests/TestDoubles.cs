using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PFound.Commerce.Core.Tests
{
    /// <summary>An in-memory wallet for the cost / reward suites.</summary>
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

    /// <summary>An in-memory entitlement store; also records "no ads".</summary>
    internal sealed class FakeEntitlementStore : IEntitlementStore
    {
        readonly HashSet<string> _owned = new HashSet<string>();

        public bool Has(string entitlementId) => _owned.Contains(entitlementId);
        public void Grant(string entitlementId) => _owned.Add(entitlementId);
        public void Revoke(string entitlementId) => _owned.Remove(entitlementId);
    }

    /// <summary>An in-memory inventory.</summary>
    internal sealed class FakeInventory : IItemInventory
    {
        readonly Dictionary<string, int> _items = new Dictionary<string, int>();

        public void Add(string itemId, int count) => _items[itemId] = Count(itemId) + count;
        public int Count(string itemId) => _items.TryGetValue(itemId, out int c) ? c : 0;
    }

    /// <summary>A rewarded-ad player with a scripted outcome and availability.</summary>
    internal sealed class StubAdPlayer : IRewardedAdPlayer
    {
        readonly RewardedAdOutcome _outcome;

        public StubAdPlayer(RewardedAdOutcome outcome, bool available = true)
        {
            _outcome = outcome;
            IsAvailable = available;
        }

        public bool IsAvailable { get; }
        public RewardedAdOutcome Show() => _outcome;
    }

    /// <summary>A fixed clock for deterministic ledger timestamps.</summary>
    internal sealed class FixedClock : ICommerceClock
    {
        public FixedClock(long now)
        {
            NowUnixSeconds = now;
        }

        public long NowUnixSeconds { get; }
    }

    /// <summary>
    /// A verifier whose answer is scripted per test. It also counts how many times it was called, so a
    /// test can prove an already-granted receipt short-circuits before re-verifying.
    /// </summary>
    internal sealed class StubVerifier : IPurchaseVerifier
    {
        readonly bool _verified;
        readonly string _canonicalOrderId;

        public int Calls { get; private set; }

        public StubVerifier(bool verified, string canonicalOrderId = null)
        {
            _verified = verified;
            _canonicalOrderId = canonicalOrderId;
        }

        public Task<PurchaseVerification> VerifyAsync(PurchaseReceipt receipt, CancellationToken cancellationToken)
        {
            Calls++;
            PurchaseVerification result = _verified
                ? PurchaseVerification.Success(_canonicalOrderId ?? receipt.OrderId)
                : PurchaseVerification.Failure("stub rejected");
            return Task.FromResult(result);
        }
    }

    /// <summary>
    /// A scripted store controller: a purchase returns a receipt for the requested SKU, restore returns a
    /// preset list, and a pending transaction can be raised to simulate interrupted-purchase recovery.
    /// </summary>
    internal sealed class StubStore : IStoreController
    {
        readonly Dictionary<string, string> _productNameBySku;
        readonly List<PurchaseReceipt> _restorable = new List<PurchaseReceipt>();

        public bool IsInitialized { get; private set; }
        public IReadOnlyList<StoreProduct> Registered { get; private set; } = new List<StoreProduct>();

        public event System.Action<PurchaseReceipt> PendingTransaction;

        public StubStore(Dictionary<string, string> productNameBySku)
        {
            _productNameBySku = productNameBySku;
        }

        public Task InitializeAsync(IReadOnlyList<StoreProduct> products, CancellationToken cancellationToken)
        {
            Registered = products;
            IsInitialized = true;
            return Task.CompletedTask;
        }

        public Task<StorePurchaseAttempt> PurchaseAsync(string storeProductId, CancellationToken cancellationToken)
        {
            string productName = _productNameBySku[storeProductId];
            var receipt = new PurchaseReceipt(productName, storeProductId, "order-" + storeProductId, StorePlatform.GooglePlay,
                "payload", 4.99m, "USD", false);
            return Task.FromResult(StorePurchaseAttempt.Success(receipt));
        }

        public void AddRestorable(PurchaseReceipt receipt) => _restorable.Add(receipt);

        public Task<IReadOnlyList<PurchaseReceipt>> RestoreAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<PurchaseReceipt>>(_restorable);

        public int FinishedCount { get; private set; }
        public string LastFinishedOrderId { get; private set; }

        public void FinishTransaction(PurchaseReceipt receipt)
        {
            FinishedCount++;
            LastFinishedOrderId = receipt.OrderId;
        }

        public string LocalizedPrice(string storeProductId) => "$4.99";

        public void RaisePending(PurchaseReceipt receipt) => PendingTransaction?.Invoke(receipt);
    }
}
