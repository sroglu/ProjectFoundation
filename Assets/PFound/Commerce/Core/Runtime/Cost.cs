using System.Collections.Generic;

namespace PFound.Commerce.Core
{
    /// <summary>
    /// What a product costs. A cost is a small immutable descriptor tagged by <see cref="Kind"/>; the
    /// behaviour (can-it-be-paid, pay-it) lives in the matching resolver, keeping the descriptor free of
    /// any wallet / store / ad dependency. The single-word name is unambiguous inside the Commerce
    /// namespace (see CODING-STYLE §8).
    /// </summary>
    public interface ICost
    {
        CostKind Kind { get; }
    }

    /// <summary>A cost that is always satisfiable and takes nothing — a free product or a claim reward.</summary>
    public sealed class FreeCost : ICost
    {
        public static readonly FreeCost Instance = new FreeCost();
        public CostKind Kind => CostKind.Free;
    }

    /// <summary>A cost paid from a soft/hard currency balance.</summary>
    public sealed class CurrencyCost : ICost
    {
        public string CurrencyId { get; }
        public long Amount { get; }

        public CurrencyCost(string currencyId, long amount)
        {
            CurrencyId = currencyId;
            Amount = amount;
        }

        public CostKind Kind => CostKind.Currency;
    }

    /// <summary>A cost paid by watching a rewarded ad to completion.</summary>
    public sealed class RewardedAdCost : ICost
    {
        public static readonly RewardedAdCost Instance = new RewardedAdCost();
        public CostKind Kind => CostKind.RewardedAd;
    }

    /// <summary>
    /// A real-money cost, settled by the store (Unity IAP) against a per-platform SKU and granted only
    /// after server verification. The Core carries the SKU reference; it never touches payment data.
    /// </summary>
    public sealed class RealMoneyCost : ICost
    {
        /// <summary>The product name whose catalog entry holds the per-platform store SKUs.</summary>
        public string ProductName { get; }

        public RealMoneyCost(string productName)
        {
            ProductName = productName;
        }

        public CostKind Kind => CostKind.RealMoney;
    }

    /// <summary>
    /// A cost where the player picks one of several options (e.g. "500 gems OR watch an ad"). The chosen
    /// branch is resolved to a concrete option before payment; each option is itself a normal cost.
    /// </summary>
    public sealed class SelectableCost : ICost
    {
        readonly ICost[] _options;

        public SelectableCost(IReadOnlyList<ICost> options)
        {
            _options = new ICost[options.Count];
            for (int i = 0; i < options.Count; i++) _options[i] = options[i];
        }

        public int OptionCount => _options.Length;
        public ICost Option(int index) => _options[index];
        public CostKind Kind => CostKind.Selectable;
    }
}
