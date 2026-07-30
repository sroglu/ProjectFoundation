using MessagePack;

namespace GameSpecific.Networking.Data
{
    /// <summary>
    /// The request DTO for <c>SpendCoins</c>: the amount of coins to deduct. A named, immutable
    /// <c>[MessagePackObject]</c> DTO (readonly struct + <c>init</c>-only keyed property) so the request is a
    /// strongly-typed wire value with an explicit, append-only <c>[Key]</c> index.
    /// </summary>
    [MessagePackObject]
    public readonly partial struct SpendRequest
    {
        [Key(0)] public int Amount { get; init; }
    }
}
