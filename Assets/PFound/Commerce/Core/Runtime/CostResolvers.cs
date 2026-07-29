using System.Collections.Generic;

namespace PFound.Commerce.Core
{
    /// <summary>
    /// Knows how to check and settle one <see cref="CostKind"/>. A resolver reports whether a cost can be
    /// paid right now (<see cref="CanPay"/>) and attempts payment (<see cref="Pay"/>), succeeding only
    /// when the cost is fully satisfied.
    /// </summary>
    public interface ICostResolver
    {
        CostKind Kind { get; }
        bool CanPay(ICost cost);
        CostPayment Pay(ICost cost);
    }

    /// <summary>
    /// The set of cost resolvers, one per <see cref="CostKind"/>, and the single dispatch point every
    /// caller routes through. A cost with no registered resolver is a configuration error: the lookup
    /// throws rather than silently doing nothing (fail-fast).
    /// </summary>
    public sealed class CostResolverRegistry
    {
        readonly Dictionary<CostKind, ICostResolver> _resolvers = new Dictionary<CostKind, ICostResolver>();

        public void Register(ICostResolver resolver)
        {
            _resolvers[resolver.Kind] = resolver;
        }

        public bool CanPay(ICost cost) => Resolver(cost.Kind).CanPay(cost);

        public CostPayment Pay(ICost cost) => Resolver(cost.Kind).Pay(cost);

        ICostResolver Resolver(CostKind kind)
        {
            if (!_resolvers.TryGetValue(kind, out ICostResolver resolver))
            {
                throw new KeyNotFoundException("No cost resolver registered for kind '" + kind + "'.");
            }
            return resolver;
        }
    }

    /// <summary>A free cost is always payable and takes nothing.</summary>
    public sealed class FreeCostResolver : ICostResolver
    {
        public CostKind Kind => CostKind.Free;
        public bool CanPay(ICost cost) => true;
        public CostPayment Pay(ICost cost) => CostPayment.Paid;
    }

    /// <summary>Pays from a currency balance; short balance leaves the wallet untouched.</summary>
    public sealed class CurrencyCostResolver : ICostResolver
    {
        readonly ICurrencyWallet _wallet;

        public CurrencyCostResolver(ICurrencyWallet wallet)
        {
            _wallet = wallet;
        }

        public CostKind Kind => CostKind.Currency;

        public bool CanPay(ICost cost)
        {
            var currency = (CurrencyCost)cost;
            return _wallet.CanSpend(currency.CurrencyId, currency.Amount);
        }

        public CostPayment Pay(ICost cost)
        {
            var currency = (CurrencyCost)cost;
            return _wallet.TrySpend(currency.CurrencyId, currency.Amount)
                ? CostPayment.Paid
                : CostPayment.InsufficientFunds;
        }
    }

    /// <summary>Pays by playing a rewarded ad; succeeds only when the ad is watched to completion.</summary>
    public sealed class RewardedAdCostResolver : ICostResolver
    {
        readonly IRewardedAdPlayer _ads;

        public RewardedAdCostResolver(IRewardedAdPlayer ads)
        {
            _ads = ads;
        }

        public CostKind Kind => CostKind.RewardedAd;

        public bool CanPay(ICost cost) => _ads.IsAvailable;

        public CostPayment Pay(ICost cost)
        {
            return _ads.Show() == RewardedAdOutcome.Watched
                ? CostPayment.Paid
                : CostPayment.AdNotWatched;
        }
    }

    /// <summary>
    /// A real-money cost cannot be settled here — it is routed to the store purchase + server-verify
    /// pipeline. The resolver exists so dispatch is uniform; its <see cref="Pay"/> signals that routing.
    /// </summary>
    public sealed class RealMoneyCostResolver : ICostResolver
    {
        public CostKind Kind => CostKind.RealMoney;
        public bool CanPay(ICost cost) => true;
        public CostPayment Pay(ICost cost) => CostPayment.RequiresStorePurchase;
    }

    /// <summary>
    /// A selectable cost is payable when at least one of its options is. It is never paid directly: the
    /// player's chosen branch is resolved to a concrete option first (see <see cref="CostCheckout"/>),
    /// then that option is paid as a normal cost — so paying the wrapper unresolved reports
    /// <see cref="CostPaymentStatus.NoChoice"/>.
    /// </summary>
    public sealed class SelectableCostResolver : ICostResolver
    {
        readonly CostResolverRegistry _registry;

        public SelectableCostResolver(CostResolverRegistry registry)
        {
            _registry = registry;
        }

        public CostKind Kind => CostKind.Selectable;

        public bool CanPay(ICost cost)
        {
            var selectable = (SelectableCost)cost;
            for (int i = 0; i < selectable.OptionCount; i++)
            {
                if (_registry.CanPay(selectable.Option(i))) return true;
            }
            return false;
        }

        public CostPayment Pay(ICost cost) => CostPayment.NoChoice;
    }
}
