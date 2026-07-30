using MessagePack;

namespace GameSpecific.Networking.Data
{
    [MessagePackObject]
    public readonly partial struct AllianceId
    {
        [Key(0)] public  long Value { get; init; }
    }
}