using MessagePack;

namespace GameSpecific.Networking.Data
{
    [MessagePackObject]
    public readonly partial struct PlayerPresence
    {
        [Key(0)] public PlayerId PlayerId { get; init; }
        [Key(1)] public bool Connected { get; init; }
    }
}