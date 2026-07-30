namespace PFound.Commerce.Core
{
    /// <summary>
    /// The player's spendable balances (soft and hard currency), keyed by a currency id. Commerce reads
    /// and mutates balances through this seam so the Core stays free of any concrete save/wallet backend;
    /// the game supplies the implementation bound to its player data.
    /// </summary>
    public interface ICurrencyWallet
    {
        long Balance(string currencyId);
        bool CanSpend(string currencyId, long amount);

        /// <summary>Spend atomically; returns false (and changes nothing) when the balance is short.</summary>
        bool TrySpend(string currencyId, long amount);

        void Deposit(string currencyId, long amount);
    }

    /// <summary>
    /// Durable one-time entitlements — owned non-consumables, active subscriptions, and boolean unlocks
    /// such as "no ads". Ownership is keyed by a stable entitlement id (a product name, or a named flag
    /// like <c>no_ads</c>). This is the authoritative-on-device mirror of the server's entitlement state.
    /// </summary>
    public interface IEntitlementStore
    {
        bool Has(string entitlementId);
        void Grant(string entitlementId);
        void Revoke(string entitlementId);
    }

    /// <summary>The player's inventory of countable items, granted by inventory-item rewards.</summary>
    public interface IItemInventory
    {
        void Add(string itemId, int count);
        int Count(string itemId);
    }

    /// <summary>
    /// Plays a rewarded ad and reports whether it was fully watched. Commerce depends only on this seam,
    /// never on a concrete ad network; the real network is a future Ads module, and tests supply a stub.
    /// A cost paid by ad succeeds only on <see cref="RewardedAdOutcome.Watched"/>.
    /// </summary>
    public interface IRewardedAdPlayer
    {
        /// <summary>True when an ad is loaded and can be shown right now (used by the cost's CanPay check).</summary>
        bool IsAvailable { get; }

        /// <summary>Show the ad and block until it resolves to watched / skipped / failed.</summary>
        RewardedAdOutcome Show();
    }

    /// <summary>Engine-free time source, so ledger timestamps are deterministic under test.</summary>
    public interface ICommerceClock
    {
        long NowUnixSeconds { get; }
    }
}
