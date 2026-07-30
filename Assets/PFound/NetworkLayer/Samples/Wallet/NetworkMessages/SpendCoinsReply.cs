namespace PFound.NetworkLayer.Samples.Wallet
{
    /// <summary>
    /// The server's reply to a <see cref="SpendCoinsRequest"/>, carrying the post-spend balance in
    /// <see cref="Outcome"/>. <see cref="Clear"/> chains <c>base.Clear()</c> to reset the reply status
    /// before wiping the payload.
    /// </summary>
    public sealed class SpendCoinsReply : ReplyMessage, IMessagePayload
    {
        public SpendOutcome Outcome;

        System.Type IMessagePayload.PayloadType => typeof(SpendOutcome);
        object IMessagePayload.Payload { get => Outcome; set => Outcome = (SpendOutcome)value; }

        public override void Clear()
        {
            base.Clear();
            Outcome = default;
        }
    }
}
