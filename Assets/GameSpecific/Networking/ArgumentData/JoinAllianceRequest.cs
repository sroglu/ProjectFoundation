using MessagePack;

namespace GameSpecific.Networking.Data
{
    /// <summary>
    /// The request DTO for <c>JoinAlliance</c>: which alliance to join. A named, immutable
    /// <c>[MessagePackObject]</c> DTO with an explicit, append-only <c>[Key]</c> index.
    /// </summary>
    [MessagePackObject]
    public readonly partial struct JoinAllianceRequest
    {
        [Key(0)] public AllianceId AllianceId { get; init; }
        [Key(1)] public PlayerId PlayerId { get; init; }
    }
}
