using MessagePack;

namespace PFound.NetworkLayer.Samples.Alliance
{
    /// <summary>
    /// The payload a client sends to join an alliance: an immutable, MessagePack-serialized value carrying
    /// the target alliance's id. Single-keyed.
    /// </summary>
    [MessagePackObject]
    public readonly partial struct JoinAlliance
    {
        [Key(0)] public long AllianceId { get; init; }
    }
}
