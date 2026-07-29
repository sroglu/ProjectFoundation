namespace PFound.Commerce.Core
{
    /// <summary>The store a product's SKU belongs to. Drives per-platform SKU resolution in the catalog.</summary>
    public enum StorePlatform
    {
        GooglePlay,
        AppStore
    }

    /// <summary>
    /// How a store product behaves. Consumables are re-purchasable and consumed on grant; non-consumables
    /// and subscriptions are one-time entitlements that can be restored on a reinstall / new device.
    /// </summary>
    public enum ProductKind
    {
        Consumable,
        NonConsumable,
        Subscription
    }

    /// <summary>
    /// The way a product is paid for. Everything except <see cref="RealMoney"/> is settled locally; a
    /// real-money cost is settled by the store transaction and only granted after server verification.
    /// </summary>
    public enum CostKind
    {
        Free,
        Currency,
        RewardedAd,
        RealMoney,
        Selectable
    }

    /// <summary>The result of showing a rewarded ad. A cost is only paid on <see cref="Watched"/>.</summary>
    public enum RewardedAdOutcome
    {
        Watched,
        Skipped,
        Failed
    }
}
