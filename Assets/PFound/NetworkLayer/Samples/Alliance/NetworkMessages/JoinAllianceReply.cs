namespace PFound.NetworkLayer.Samples.Alliance
{
    /// <summary>
    /// The server's reply to a <see cref="JoinAllianceRequest"/>, carrying the post-join roster state
    /// in <see cref="Outcome"/>. <see cref="Clear"/> chains <c>base.Clear()</c> to reset the reply
    /// status before wiping the payload.
    /// </summary>
    public sealed class JoinAllianceReply : ReplyMessage
    {
        public JoinOutcome Outcome;

        public override void Clear()
        {
            base.Clear();
            Outcome = default;
        }
    }
}
