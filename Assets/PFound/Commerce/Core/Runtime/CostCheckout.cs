using System;

namespace PFound.Commerce.Core
{
    /// <summary>How a non-store checkout ended.</summary>
    public enum CheckoutStatus
    {
        /// <summary>The cost was paid and the rewards granted.</summary>
        Purchased,

        /// <summary>The cost could not be paid (short balance / ad not watched).</summary>
        CannotPay,

        /// <summary>A selectable product was bought without a valid option chosen.</summary>
        NoChoice,

        /// <summary>The product is not in the catalog (config/programmer error, surfaced).</summary>
        UnknownProduct,

        /// <summary>
        /// The product is real-money; hand it to the store purchase + server-verify pipeline. Not a
        /// failure — a routing signal for the Unity layer.
        /// </summary>
        RequiresStorePurchase
    }

    /// <summary>The result of a non-store checkout.</summary>
    public readonly struct CheckoutResult
    {
        public CheckoutStatus Status { get; }
        public string ProductName { get; }
        public CostPaymentStatus PaymentStatus { get; }

        public CheckoutResult(CheckoutStatus status, string productName, CostPaymentStatus paymentStatus)
        {
            Status = status;
            ProductName = productName;
            PaymentStatus = paymentStatus;
        }

        public bool Purchased => Status == CheckoutStatus.Purchased;
    }

    /// <summary>
    /// Buys a product whose cost is settled locally — currency, a rewarded ad, free, or a player-chosen
    /// selectable option. It resolves the product from the catalog, resolves the chosen branch of a
    /// selectable cost, pays the cost, and (only on a paid cost) applies the reward set. Real-money costs
    /// are not settled here: they route to <see cref="PurchasePipeline"/> so a grant always follows a
    /// server-verified purchase. An unknown product fails cleanly.
    /// </summary>
    public sealed class CostCheckout
    {
        readonly CostResolverRegistry _costs;
        readonly RewardResolverRegistry _rewards;

        ProductCatalog _catalog;

        /// <summary>Raised after a local checkout grants its rewards.</summary>
        public event Action<ProductDefinition> Purchased;

        /// <summary>Raised with an analytics event to forward to the game's analytics provider.</summary>
        public event Action<CommerceAnalyticsEvent> AnalyticsEmitted;

        public CostCheckout(CostResolverRegistry costs, RewardResolverRegistry rewards, ProductCatalog catalog)
        {
            _costs = costs;
            _rewards = rewards;
            _catalog = catalog;
        }

        public void UpdateCatalog(ProductCatalog catalog)
        {
            _catalog = catalog;
        }

        /// <summary>True when the product's cost can be paid right now (drives affordability UI).</summary>
        public bool CanAfford(string productName)
        {
            return _catalog.TryGet(productName, out ProductDefinition product) && _costs.CanPay(product.Cost);
        }

        /// <summary>
        /// Buy <paramref name="productName"/>. For a selectable cost, <paramref name="chosenOption"/>
        /// selects the branch (ignored otherwise). Real-money products return
        /// <see cref="CheckoutStatus.RequiresStorePurchase"/> for the Unity IAP layer to handle.
        /// </summary>
        public CheckoutResult Buy(string productName, int chosenOption = -1)
        {
            if (!_catalog.TryGet(productName, out ProductDefinition product))
            {
                return new CheckoutResult(CheckoutStatus.UnknownProduct, productName, default);
            }

            if (product.Cost.Kind == CostKind.RealMoney)
            {
                return new CheckoutResult(CheckoutStatus.RequiresStorePurchase, productName, CostPaymentStatus.RequiresStorePurchase);
            }

            AnalyticsEmitted?.Invoke(CommerceAnalyticsEvent.PurchaseStarted(productName));

            if (!TryResolveEffectiveCost(product.Cost, chosenOption, out ICost effective))
            {
                AnalyticsEmitted?.Invoke(CommerceAnalyticsEvent.PurchaseFailed(productName, "no option chosen"));
                return new CheckoutResult(CheckoutStatus.NoChoice, productName, CostPaymentStatus.NoChoice);
            }

            CostPayment payment = _costs.Pay(effective);
            if (!payment.Succeeded)
            {
                AnalyticsEmitted?.Invoke(CommerceAnalyticsEvent.PurchaseFailed(productName, payment.Status.ToString()));
                return new CheckoutResult(CheckoutStatus.CannotPay, productName, payment.Status);
            }

            product.Rewards.ApplyThrough(_rewards);
            Purchased?.Invoke(product);
            return new CheckoutResult(CheckoutStatus.Purchased, productName, CostPaymentStatus.Paid);
        }

        static bool TryResolveEffectiveCost(ICost cost, int chosenOption, out ICost effective)
        {
            if (cost.Kind != CostKind.Selectable)
            {
                effective = cost;
                return true;
            }

            var selectable = (SelectableCost)cost;
            if (chosenOption < 0 || chosenOption >= selectable.OptionCount)
            {
                effective = null;
                return false;
            }

            effective = selectable.Option(chosenOption);
            return true;
        }
    }
}
