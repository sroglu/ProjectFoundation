using System.Collections.Generic;

namespace PFound.Commerce.Core
{
    /// <summary>
    /// A commerce analytics event, emitted as a plain value so Commerce depends on no analytics backend.
    /// The game subscribes to the emit signal and forwards <see cref="Name"/> + <see cref="Properties"/>
    /// straight to its analytics provider (the shapes match the provider's Track(name, properties) seam).
    /// This keeps Commerce free of any wholesale analytics-assembly dependency.
    /// </summary>
    public readonly struct CommerceAnalyticsEvent
    {
        public const string PurchaseStartedName = "purchase_started";
        public const string PurchaseCompletedName = "purchase_completed";
        public const string PurchaseFailedName = "purchase_failed";

        public string Name { get; }
        public IReadOnlyDictionary<string, object> Properties { get; }

        CommerceAnalyticsEvent(string name, IReadOnlyDictionary<string, object> properties)
        {
            Name = name;
            Properties = properties;
        }

        public static CommerceAnalyticsEvent PurchaseStarted(string productName)
        {
            return new CommerceAnalyticsEvent(PurchaseStartedName, new Dictionary<string, object>
            {
                { "product", productName }
            });
        }

        public static CommerceAnalyticsEvent PurchaseCompleted(string productName, decimal price, string currencyCode)
        {
            return new CommerceAnalyticsEvent(PurchaseCompletedName, new Dictionary<string, object>
            {
                { "product", productName },
                { "price", price },
                { "currency", currencyCode }
            });
        }

        public static CommerceAnalyticsEvent PurchaseFailed(string productName, string reason)
        {
            return new CommerceAnalyticsEvent(PurchaseFailedName, new Dictionary<string, object>
            {
                { "product", productName },
                { "reason", reason }
            });
        }
    }
}
