using MessagePack;

namespace PFound.NetworkLayer.Samples.Wallet
{
    /// <summary>
    /// The payload a client sends to spend coins: an immutable, MessagePack-serialized value carrying just
    /// the amount to deduct. Single-keyed — the simplest shape a content DTO takes. Authored as a
    /// <c>readonly partial struct</c> with an <c>init</c>-only keyed property; the PFound generator adds value
    /// equality + <c>ToString</c>, MessagePack's generator adds the wire formatter.
    /// </summary>
    [MessagePackObject]
    public readonly partial struct SpendCoins
    {
        [Key(0)] public int Amount { get; init; }
    }
}
