using System.Collections.Generic;
using GameSpecific.Networking.Data.Item;
using MessagePack;

namespace GameSpecific.Networking.Data
{
    [MessagePackObject]
    public readonly partial struct RewardContainer
    {
        [Key(0)] public PlayerId PlayerId { get; init; }
        [Key(1)] public IReadOnlyList<RewardItem> RewardItems { get; init; }
    }
}