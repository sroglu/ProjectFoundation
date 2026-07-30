using MessagePack;
using PFound.NetworkLayer;

namespace GameSpecific.Networking.Data
{
    [MessagePackObject]
    [Reserved(2, 1.01)]
    [Reserved(6)]
    [Reserved(7)]
    [Reserved(8)]
    [Reserved(9)]
    public readonly partial struct PlayerData
    {
        [Key(0)] public PlayerId PlayerId { get; init; }
        [Key(1)] public ushort Level { get; init; }
        [Key(5)] public string Name { get; init; }
        [Key(10)] public long Coins { get; init; }
    }
}