using System;
using System.Collections.Generic;

namespace PFound.Commerce.Core
{
    /// <summary>Applies one reward type to the player's state (wallet, inventory, entitlements, …).</summary>
    public interface IRewardResolver
    {
        string RewardType { get; }
        void Apply(IReward reward);
    }

    /// <summary>
    /// The set of reward resolvers keyed by reward type, and the single point that applies a reward and
    /// raises the <see cref="RewardApplied"/> signal so analytics / UI can react. A reward whose type has
    /// no resolver is a configuration error and throws (fail-fast).
    /// </summary>
    public sealed class RewardResolverRegistry
    {
        readonly Dictionary<string, IRewardResolver> _resolvers = new Dictionary<string, IRewardResolver>();

        /// <summary>Raised after each reward is applied, so a listener can update UI or record analytics.</summary>
        public event Action<IReward> RewardApplied;

        public void Register(IRewardResolver resolver)
        {
            _resolvers[resolver.RewardType] = resolver;
        }

        public void Apply(IReward reward)
        {
            if (!_resolvers.TryGetValue(reward.RewardType, out IRewardResolver resolver))
            {
                throw new KeyNotFoundException("No reward resolver registered for type '" + reward.RewardType + "'.");
            }
            resolver.Apply(reward);
            RewardApplied?.Invoke(reward);
        }

        public void ApplyAll(RewardSet rewards)
        {
            for (int i = 0; i < rewards.Count; i++) Apply(rewards[i]);
        }
    }

    /// <summary>Deposits a currency reward into the wallet.</summary>
    public sealed class CurrencyRewardResolver : IRewardResolver
    {
        readonly ICurrencyWallet _wallet;

        public CurrencyRewardResolver(ICurrencyWallet wallet)
        {
            _wallet = wallet;
        }

        public string RewardType => RewardTypes.Currency;

        public void Apply(IReward reward)
        {
            var currency = (CurrencyReward)reward;
            _wallet.Deposit(currency.CurrencyId, currency.Amount);
        }
    }

    /// <summary>Adds an inventory-item reward to the inventory.</summary>
    public sealed class InventoryItemRewardResolver : IRewardResolver
    {
        readonly IItemInventory _inventory;

        public InventoryItemRewardResolver(IItemInventory inventory)
        {
            _inventory = inventory;
        }

        public string RewardType => RewardTypes.InventoryItem;

        public void Apply(IReward reward)
        {
            var item = (InventoryItemReward)reward;
            _inventory.Add(item.ItemId, item.Count);
        }
    }

    /// <summary>Grants an entitlement (unlock / subscription / the "no ads" flag).</summary>
    public sealed class EntitlementRewardResolver : IRewardResolver
    {
        readonly IEntitlementStore _entitlements;

        public EntitlementRewardResolver(IEntitlementStore entitlements)
        {
            _entitlements = entitlements;
        }

        public string RewardType => RewardTypes.Entitlement;

        public void Apply(IReward reward)
        {
            var entitlement = (EntitlementReward)reward;
            _entitlements.Grant(entitlement.EntitlementId);
        }
    }
}
