using System.Collections.Generic;

namespace PFound.Commerce.Core
{
    /// <summary>
    /// The set of products the game sells, keyed by product name, with a reverse index from each
    /// platform's store SKU back to the product (so a store receipt, which names the SKU, resolves to a
    /// catalog entry). Immutable; rebuilt whenever the remote catalog changes. Looking up an unknown
    /// product returns false so the caller can fail cleanly — a purchase of an unknown product is a
    /// config/programmer error surfaced, never a silent no-op.
    /// </summary>
    public sealed class ProductCatalog
    {
        public static readonly ProductCatalog Empty = new ProductCatalog(new ProductDefinition[0]);

        readonly Dictionary<string, ProductDefinition> _byName;
        readonly Dictionary<StorePlatform, Dictionary<string, ProductDefinition>> _byStoreId;

        public ProductCatalog(IReadOnlyList<ProductDefinition> products)
        {
            _byName = new Dictionary<string, ProductDefinition>(products.Count);
            _byStoreId = new Dictionary<StorePlatform, Dictionary<string, ProductDefinition>>();

            for (int i = 0; i < products.Count; i++)
            {
                ProductDefinition product = products[i];
                _byName[product.ProductName] = product;

                foreach (KeyValuePair<StorePlatform, string> pair in product.StoreIds)
                {
                    if (!_byStoreId.TryGetValue(pair.Key, out Dictionary<string, ProductDefinition> map))
                    {
                        map = new Dictionary<string, ProductDefinition>();
                        _byStoreId[pair.Key] = map;
                    }
                    map[pair.Value] = product;
                }
            }
        }

        public int Count => _byName.Count;
        public IEnumerable<ProductDefinition> Products => _byName.Values;

        public bool TryGet(string productName, out ProductDefinition product) => _byName.TryGetValue(productName, out product);

        public bool Contains(string productName) => _byName.ContainsKey(productName);

        /// <summary>Resolve a store SKU on a platform back to its product (used when a receipt names the SKU).</summary>
        public bool TryGetByStoreId(StorePlatform platform, string storeId, out ProductDefinition product)
        {
            product = null;
            return _byStoreId.TryGetValue(platform, out Dictionary<string, ProductDefinition> map)
                && map.TryGetValue(storeId, out product);
        }

        /// <summary>All distinct store SKUs on a platform — the set the store is initialised with.</summary>
        public IEnumerable<string> StoreIdsFor(StorePlatform platform)
        {
            return _byStoreId.TryGetValue(platform, out Dictionary<string, ProductDefinition> map)
                ? map.Keys
                : new string[0];
        }
    }
}
