using System.Collections.Generic;

namespace PFound.Commerce.Core
{
    /// <summary>
    /// One catalog entry: what the product is called, how it is paid for, what it grants, its store
    /// behaviour and category, and its per-platform store SKUs. Built from remote config, so pricing and
    /// bundling change without an app update. Store SKUs are only present for real-money products.
    /// </summary>
    public sealed class ProductDefinition
    {
        readonly Dictionary<StorePlatform, string> _storeIds;

        public string ProductName { get; }
        public string Category { get; }
        public ProductKind Kind { get; }
        public ICost Cost { get; }
        public RewardSet Rewards { get; }

        public ProductDefinition(
            string productName,
            string category,
            ProductKind kind,
            ICost cost,
            RewardSet rewards,
            IReadOnlyDictionary<StorePlatform, string> storeIds)
        {
            ProductName = productName;
            Category = category;
            Kind = kind;
            Cost = cost;
            Rewards = rewards;
            _storeIds = new Dictionary<StorePlatform, string>(storeIds.Count);
            foreach (KeyValuePair<StorePlatform, string> pair in storeIds) _storeIds[pair.Key] = pair.Value;
        }

        /// <summary>The store SKU for the given platform. Absent for a non-store (currency/ad/free) cost.</summary>
        public bool TryGetStoreId(StorePlatform platform, out string storeId) => _storeIds.TryGetValue(platform, out storeId);

        public IReadOnlyDictionary<StorePlatform, string> StoreIds => _storeIds;
    }
}
