using MessagePack;

namespace GameSpecific.Networking.Data.Item
{
    [MessagePackObject]
    public readonly partial struct ItemId
    {
        [Key(0)] public int Value { get; init; }
    }
}