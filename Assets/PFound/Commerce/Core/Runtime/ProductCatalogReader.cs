using System;
using System.Collections.Generic;

namespace PFound.Commerce.Core
{
    /// <summary>
    /// Builds a <see cref="ProductCatalog"/> from the remote-config catalog JSON. The document is a
    /// "small tuning table" served by RemoteGameConfig; this reader turns it into the typed catalog model.
    /// Malformed JSON yields <c>false</c> (the caller keeps its last-good / embedded catalog), consistent
    /// with the config module's parse tolerance. The reward set is open: pass custom reward factories to
    /// parse a game's own reward types alongside the built-ins.
    ///
    /// Shape:
    /// <code>
    /// { "products": {
    ///     "coins_small": {
    ///       "category": "currency", "kind": "consumable",
    ///       "cost": { "kind": "realmoney" },
    ///       "sku": { "googleplay": "com.game.coins_small", "appstore": "com.game.coins_small" },
    ///       "rewards": [ { "type": "currency", "id": "coins", "amount": 100 } ] },
    ///     "double_reward": {
    ///       "cost": { "kind": "rewardedad" },
    ///       "rewards": [ { "type": "currency", "id": "coins", "amount": 20 } ] } } }
    /// </code>
    /// </summary>
    public static class ProductCatalogReader
    {
        /// <summary>Parse the catalog JSON using only the built-in reward types.</summary>
        public static bool TryParse(string json, out ProductCatalog catalog)
            => TryParse(json, new Dictionary<string, Func<JsonValue, IReward>>(0), out catalog);

        /// <summary>
        /// Parse the catalog JSON, resolving reward types first through <paramref name="customRewards"/>
        /// (a game's own types), then the built-ins. An unknown reward or cost kind fails the parse.
        /// </summary>
        public static bool TryParse(string json, IReadOnlyDictionary<string, Func<JsonValue, IReward>> customRewards, out ProductCatalog catalog)
        {
            catalog = null;
            if (!JsonValue.Parse(json, out JsonValue root)) return false;
            if (!root.TryGet("products", out JsonValue products) || products.Kind != JsonKind.Object) return false;

            var definitions = new List<ProductDefinition>(products.Count);
            foreach (string productName in products.Keys)
            {
                products.TryGet(productName, out JsonValue node);
                if (!TryReadProduct(productName, node, customRewards, out ProductDefinition definition)) return false;
                definitions.Add(definition);
            }

            catalog = new ProductCatalog(definitions);
            return true;
        }

        static bool TryReadProduct(string productName, JsonValue node, IReadOnlyDictionary<string, Func<JsonValue, IReward>> customRewards, out ProductDefinition definition)
        {
            definition = null;
            if (node.Kind != JsonKind.Object) return false;

            string category = node.GetString("category", string.Empty);
            if (!TryReadKind(node.GetString("kind", "consumable"), out ProductKind kind)) return false;
            if (!node.TryGet("cost", out JsonValue costNode) || !TryReadCost(productName, costNode, out ICost cost)) return false;
            if (!TryReadRewards(node, customRewards, out RewardSet rewards)) return false;

            var storeIds = new Dictionary<StorePlatform, string>();
            if (node.TryGet("sku", out JsonValue sku) && sku.Kind == JsonKind.Object)
            {
                if (sku.Has("googleplay")) storeIds[StorePlatform.GooglePlay] = sku.GetString("googleplay", string.Empty);
                if (sku.Has("appstore")) storeIds[StorePlatform.AppStore] = sku.GetString("appstore", string.Empty);
            }

            definition = new ProductDefinition(productName, category, kind, cost, rewards, storeIds);
            return true;
        }

        static bool TryReadKind(string kind, out ProductKind result)
        {
            switch (kind)
            {
                case "consumable": result = ProductKind.Consumable; return true;
                case "nonconsumable": result = ProductKind.NonConsumable; return true;
                case "subscription": result = ProductKind.Subscription; return true;
                default: result = ProductKind.Consumable; return false;
            }
        }

        static bool TryReadCost(string productName, JsonValue node, out ICost cost)
        {
            cost = null;
            if (node.Kind != JsonKind.Object) return false;

            switch (node.GetString("kind", string.Empty))
            {
                case "free":
                    cost = FreeCost.Instance;
                    return true;
                case "currency":
                    cost = new CurrencyCost(node.GetString("currencyId", string.Empty), node.GetLong("amount", 0));
                    return true;
                case "rewardedad":
                    cost = RewardedAdCost.Instance;
                    return true;
                case "realmoney":
                    cost = new RealMoneyCost(productName);
                    return true;
                case "selectable":
                    return TryReadSelectable(productName, node, out cost);
                default:
                    return false;
            }
        }

        static bool TryReadSelectable(string productName, JsonValue node, out ICost cost)
        {
            cost = null;
            if (!node.TryGet("options", out JsonValue options) || options.Kind != JsonKind.Array || options.Count == 0) return false;

            var branches = new List<ICost>(options.Count);
            for (int i = 0; i < options.Count; i++)
            {
                if (!TryReadCost(productName, options.At(i), out ICost branch)) return false;
                branches.Add(branch);
            }

            cost = new SelectableCost(branches);
            return true;
        }

        static bool TryReadRewards(JsonValue node, IReadOnlyDictionary<string, Func<JsonValue, IReward>> customRewards, out RewardSet rewards)
        {
            rewards = RewardSet.Empty;
            if (!node.TryGet("rewards", out JsonValue list)) return true; // a product may grant nothing but an entitlement
            if (list.Kind != JsonKind.Array) return false;

            var parsed = new List<IReward>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                if (!TryReadReward(list.At(i), customRewards, out IReward reward)) return false;
                parsed.Add(reward);
            }

            rewards = new RewardSet(parsed);
            return true;
        }

        static bool TryReadReward(JsonValue node, IReadOnlyDictionary<string, Func<JsonValue, IReward>> customRewards, out IReward reward)
        {
            reward = null;
            if (node.Kind != JsonKind.Object) return false;

            string type = node.GetString("type", string.Empty);
            if (customRewards.TryGetValue(type, out Func<JsonValue, IReward> factory))
            {
                reward = factory(node);
                return true;
            }

            switch (type)
            {
                case RewardTypes.Currency:
                    reward = new CurrencyReward(node.GetString("id", string.Empty), node.GetLong("amount", 0));
                    return true;
                case RewardTypes.InventoryItem:
                    reward = new InventoryItemReward(node.GetString("id", string.Empty), node.GetInt("count", 1));
                    return true;
                case RewardTypes.Entitlement:
                    reward = new EntitlementReward(node.GetString("id", string.Empty));
                    return true;
                default:
                    return false;
            }
        }
    }
}
