namespace PFound.Commerce.Core
{
    /// <summary>
    /// The evidence of a completed store transaction, handed to the verifier and (once verified) recorded
    /// in the ledger. The client never trusts it on its own — the payload is what the server validates
    /// against Google Play / Apple. Carries the store order-id / purchase-token used for idempotent dedup,
    /// and a flag distinguishing a fresh user purchase from a restored / recovered one.
    /// </summary>
    public sealed class PurchaseReceipt
    {
        /// <summary>The catalog product name this transaction is for (resolved from the store SKU).</summary>
        public string ProductName { get; }

        /// <summary>The platform's store product id (SKU) that was purchased.</summary>
        public string StoreProductId { get; }

        /// <summary>The store order-id / purchase-token — the idempotency key that grants exactly once.</summary>
        public string OrderId { get; }

        public StorePlatform Platform { get; }

        /// <summary>The opaque store receipt payload the server validates. Never inspected on the client.</summary>
        public string Payload { get; }

        /// <summary>The price paid, in the store's localized currency, for the ledger + revenue analytics.</summary>
        public decimal Price { get; }

        /// <summary>ISO-4217 currency code of <see cref="Price"/>.</summary>
        public string CurrencyCode { get; }

        /// <summary>True when this receipt came from a restore / recovery flow rather than a fresh purchase.</summary>
        public bool IsRestored { get; }

        public PurchaseReceipt(
            string productName,
            string storeProductId,
            string orderId,
            StorePlatform platform,
            string payload,
            decimal price,
            string currencyCode,
            bool isRestored)
        {
            ProductName = productName;
            StoreProductId = storeProductId;
            OrderId = orderId;
            Platform = platform;
            Payload = payload;
            Price = price;
            CurrencyCode = currencyCode;
            IsRestored = isRestored;
        }
    }
}
