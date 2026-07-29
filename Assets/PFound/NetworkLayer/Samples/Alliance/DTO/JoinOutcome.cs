using System;
using MessagePack;

namespace PFound.NetworkLayer.Samples.Alliance
{
    /// <summary>
    /// The result the server returns for a join: the alliance's member count after the join and the
    /// rank the new member was granted. Two keyed fields — showing the <c>[Key(n)]</c> scheme scale
    /// past a single field.
    /// </summary>
    [MessagePackObject]
    public readonly struct JoinOutcome : IEquatable<JoinOutcome>
    {
        [Key(0)] public readonly int MemberCount;
        [Key(1)] public readonly byte Rank;

        [SerializationConstructor]
        public JoinOutcome(int memberCount, byte rank)
        {
            MemberCount = memberCount;
            Rank = rank;
        }

        public bool Equals(JoinOutcome other) => MemberCount == other.MemberCount && Rank == other.Rank;
        public override bool Equals(object obj) => obj is JoinOutcome other && Equals(other);
        public override int GetHashCode() => (MemberCount * 397) ^ Rank;
    }
}
