namespace PFound.NetworkLayer.Samples.Wallet
{
    /// <summary>
    /// The poolable envelope a client sends to spend coins. The wire opcode and pooling live on the
    /// envelope; the serialized payload rides in <see cref="Coins"/>. <see cref="Clear"/> wipes the
    /// payload so a recycled instance never leaks the previous request.
    /// </summary>
    public sealed class SpendCoinsRequest : RequestMessage
    {
        public SpendCoins Coins;

        public override void Clear()
        {
            Coins = default;
        }
    }
}
