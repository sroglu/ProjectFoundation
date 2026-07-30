using MessagePack;

namespace GameSpecific.Networking.Data.Item
{
    [MessagePackObject]
    public readonly partial struct RewardItem
    {
        [Key(0)] public ItemId ItemId { get; init; }
        [Key(1)] public int Amount { get; init; }
    }
}