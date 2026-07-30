using MessagePack;

namespace PFound.NetworkLayer.Samples.Alliance
{
    /// <summary>
    /// The result the server returns for a join: the alliance's member count after the join and the rank the
    /// new member was granted. Two keyed members — showing the <c>[Key(n)]</c> scheme scale past a single one.
    /// </summary>
    [MessagePackObject]
    public readonly partial struct JoinOutcome
    {
        [Key(0)] public int MemberCount { get; init; }
        [Key(1)] public byte Rank { get; init; }
    }
}
