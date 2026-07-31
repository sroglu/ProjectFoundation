using MessagePack;

namespace GameSpecific.Networking.Data
{
    /// <summary>
    /// The reply DTO for <c>JoinAllianceOperation</c> — a single-element DTO carrying the roster size after joining.
    /// Same reason as <see cref="SpendResult"/>: a reply is ALWAYS a DTO struct, never a bare primitive, so
    /// even one value is wrapped in a named <c>[Dto]</c> with an explicit wire <c>[Field]</c> index that can
    /// grow more fields append-only.
    /// </summary>
    [MessagePackObject]
    public readonly partial struct AllianceJoinResult
    {
        [Key(0)] public AllianceId Id { get; init; }
        [Key(1)] public long MemberCount  { get; init; }
    }
}
