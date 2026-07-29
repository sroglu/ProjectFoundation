namespace PFound.Commerce.Core
{
    /// <summary>The outcome of attempting to pay a cost.</summary>
    public enum CostPaymentStatus
    {
        /// <summary>Fully satisfied — the caller may grant the rewards.</summary>
        Paid,

        /// <summary>Currency balance was short; nothing was spent.</summary>
        InsufficientFunds,

        /// <summary>The rewarded ad was not watched to completion (skipped / failed / no fill).</summary>
        AdNotWatched,

        /// <summary>A selectable cost was paid without a branch chosen first.</summary>
        NoChoice,

        /// <summary>
        /// A real-money cost cannot be settled locally; the caller must run the store purchase +
        /// server-verify flow. This is a routing signal, not a failure.
        /// </summary>
        RequiresStorePurchase
    }

    /// <summary>The result of a <see cref="ICostResolver.Pay"/> attempt.</summary>
    public readonly struct CostPayment
    {
        public CostPaymentStatus Status { get; }

        CostPayment(CostPaymentStatus status)
        {
            Status = status;
        }

        public bool Succeeded => Status == CostPaymentStatus.Paid;

        public static readonly CostPayment Paid = new CostPayment(CostPaymentStatus.Paid);
        public static readonly CostPayment InsufficientFunds = new CostPayment(CostPaymentStatus.InsufficientFunds);
        public static readonly CostPayment AdNotWatched = new CostPayment(CostPaymentStatus.AdNotWatched);
        public static readonly CostPayment NoChoice = new CostPayment(CostPaymentStatus.NoChoice);
        public static readonly CostPayment RequiresStorePurchase = new CostPayment(CostPaymentStatus.RequiresStorePurchase);
    }
}
