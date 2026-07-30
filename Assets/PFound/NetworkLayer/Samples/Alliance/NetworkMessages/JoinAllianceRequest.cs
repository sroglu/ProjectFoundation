namespace PFound.NetworkLayer.Samples.Alliance
{
    /// <summary>
    /// The poolable envelope a client sends to join an alliance. The serialized payload rides in
    /// <see cref="Join"/>; <see cref="Clear"/> wipes it so a recycled instance never leaks the previous
    /// request.
    /// </summary>
    public sealed class JoinAllianceRequest : RequestMessage, IMessagePayload
    {
        public JoinAlliance Join;

        System.Type IMessagePayload.PayloadType => typeof(JoinAlliance);
        object IMessagePayload.Payload { get => Join; set => Join = (JoinAlliance)value; }

        public override void Clear()
        {
            Join = default;
        }
    }
}
