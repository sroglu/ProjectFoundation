namespace PFound.Utilities.Messaging
{
    /// <summary>
    /// How long a subscription remains attached to a channel.
    /// </summary>
    public enum SubscriptionLifetime
    {
        /// <summary>Stays attached until it is explicitly removed or its guard dies.</summary>
        Persistent = 0,

        /// <summary>Detaches itself immediately after it is invoked a single time.</summary>
        FireOnce = 1,
    }
}
