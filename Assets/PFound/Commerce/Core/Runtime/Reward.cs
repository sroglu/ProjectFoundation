namespace PFound.Commerce.Core
{
    /// <summary>
    /// A single thing a product grants. The reward set is open-ended — the game may add its own kinds —
    /// so a reward is tagged by a string <see cref="RewardType"/> rather than a closed enum, and each
    /// type is applied by a registered resolver. The single-word name is unambiguous inside the Commerce
    /// namespace (see CODING-STYLE §8).
    /// </summary>
    public interface IReward
    {
        /// <summary>The type key that selects the resolver which applies this reward.</summary>
        string RewardType { get; }

        /// <summary>A short human-readable line for the ledger / analytics (e.g. "currency:coins x100").</summary>
        string Describe();
    }

    /// <summary>
    /// The built-in reward type keys. The set is open: a game registers its own key + resolver (e.g. a
    /// distinct capacity/level-end-bundle reward) on the <see cref="RewardResolverRegistry"/>, and a
    /// custom catalog-reward factory on the <see cref="ProductCatalogReader"/>. Life/energy is typically
    /// modelled as a currency ("energy"); a no-ads unlock and subscriptions are entitlements.
    /// </summary>
    public static class RewardTypes
    {
        public const string Currency = "currency";
        public const string InventoryItem = "item";
        public const string Entitlement = "entitlement";
    }

    /// <summary>Grants an amount of a currency.</summary>
    public sealed class CurrencyReward : IReward
    {
        public string CurrencyId { get; }
        public long Amount { get; }

        public CurrencyReward(string currencyId, long amount)
        {
            CurrencyId = currencyId;
            Amount = amount;
        }

        public string RewardType => RewardTypes.Currency;
        public string Describe() => RewardTypes.Currency + ":" + CurrencyId + " x" + Amount;
    }

    /// <summary>Grants a count of an inventory item.</summary>
    public sealed class InventoryItemReward : IReward
    {
        public string ItemId { get; }
        public int Count { get; }

        public InventoryItemReward(string itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }

        public string RewardType => RewardTypes.InventoryItem;
        public string Describe() => RewardTypes.InventoryItem + ":" + ItemId + " x" + Count;
    }

    /// <summary>
    /// Grants (or, when revoked, removes) a boolean entitlement — a non-consumable unlock, an active
    /// subscription, or a named flag such as "no ads" (which disables interstitials when set).
    /// </summary>
    public sealed class EntitlementReward : IReward
    {
        public string EntitlementId { get; }

        public EntitlementReward(string entitlementId)
        {
            EntitlementId = entitlementId;
        }

        public string RewardType => RewardTypes.Entitlement;
        public string Describe() => RewardTypes.Entitlement + ":" + EntitlementId;
    }
}
