using MessagePack;

namespace GameSpecific.Networking.Data
{
    /// <summary>
    /// The reply DTO for <c>SpendCoinsOperation</c> — a single-element DTO wrapping the authoritative post-spend
    /// balance. It exists to honour the hard rule: a reply is ALWAYS a DTO, never a bare primitive. Even a
    /// one-value response gets a named <c>[MessagePackObject]</c> so the member has an explicit wire
    /// <c>[Key]</c> index, stays append-only forward/backward compatible, and can grow a second value later
    /// without a breaking signature change (a loose <c>long</c> could do none of that). Authored as an
    /// immutable <c>readonly struct</c> with <c>init</c>-only keyed properties (the generator adds value
    /// equality + <c>ToString</c>; MessagePack's source generator produces the wire formatter).
    /// </summary>
    [MessagePackObject]
    public readonly partial struct SpendResult
    {
        [Key(0)] public long NewBalance { get; init; }
    }
}
