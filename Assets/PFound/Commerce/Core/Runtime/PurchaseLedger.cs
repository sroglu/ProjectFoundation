using System.Collections.Generic;

namespace PFound.Commerce.Core
{
    /// <summary>
    /// One audit-grade record of a verified purchase: what was bought, on which SKU, its order-id, the
    /// price paid, when, and the rewards granted. Immutable. Kept separate from player-save data — the
    /// ledger is the only home for purchase/receipt records (no receipt data lives in the save).
    /// </summary>
    public sealed class PurchaseLedgerEntry
    {
        public string ProductName { get; }
        public string StoreProductId { get; }
        public string OrderId { get; }
        public decimal Price { get; }
        public string CurrencyCode { get; }
        public long PurchasedAtUnixSeconds { get; }
        public bool WasRestored { get; }
        public IReadOnlyList<string> GrantedRewards { get; }

        public PurchaseLedgerEntry(
            string productName,
            string storeProductId,
            string orderId,
            decimal price,
            string currencyCode,
            long purchasedAtUnixSeconds,
            bool wasRestored,
            IReadOnlyList<string> grantedRewards)
        {
            ProductName = productName;
            StoreProductId = storeProductId;
            OrderId = orderId;
            Price = price;
            CurrencyCode = currencyCode;
            PurchasedAtUnixSeconds = purchasedAtUnixSeconds;
            WasRestored = wasRestored;
            GrantedRewards = grantedRewards;
        }
    }

    /// <summary>
    /// The client-side contract over the append-only purchase ledger. It records verified purchases and
    /// answers whether an order-id has already been granted — the check that makes granting idempotent.
    /// The durable, authoritative ledger lives server-side; this is the on-device mirror / dedup index.
    /// </summary>
    public interface IPurchaseLedger
    {
        /// <summary>True when this order-id was already recorded — the replay/duplicate guard.</summary>
        bool Contains(string orderId);

        /// <summary>Append a new entry. Callers must dedup via <see cref="Contains"/> first (append-only).</summary>
        void Append(PurchaseLedgerEntry entry);

        bool TryGet(string orderId, out PurchaseLedgerEntry entry);

        IReadOnlyList<PurchaseLedgerEntry> Entries { get; }
    }

    /// <summary>
    /// An in-memory append-only ledger — the default client mirror, and the ledger used by the Core test
    /// suite. Order-id dedup is exact; appending a duplicate order-id is a programmer error (callers must
    /// check <see cref="Contains"/>), so it throws rather than silently overwriting.
    /// </summary>
    public sealed class InMemoryPurchaseLedger : IPurchaseLedger
    {
        readonly Dictionary<string, PurchaseLedgerEntry> _byOrderId = new Dictionary<string, PurchaseLedgerEntry>();
        readonly List<PurchaseLedgerEntry> _entries = new List<PurchaseLedgerEntry>();

        public bool Contains(string orderId) => _byOrderId.ContainsKey(orderId);

        public void Append(PurchaseLedgerEntry entry)
        {
            _byOrderId.Add(entry.OrderId, entry); // throws on duplicate — append-only invariant
            _entries.Add(entry);
        }

        public bool TryGet(string orderId, out PurchaseLedgerEntry entry) => _byOrderId.TryGetValue(orderId, out entry);

        public IReadOnlyList<PurchaseLedgerEntry> Entries => _entries;
    }
}
