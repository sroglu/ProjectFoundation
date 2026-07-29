using System;
using MessagePack;

namespace PFound.NetworkLayer.Samples.Alliance
{
    /// <summary>
    /// The payload a client sends to join an alliance: an immutable, MessagePack-serialized value
    /// carrying the target alliance's id. Single-keyed.
    /// </summary>
    [MessagePackObject]
    public readonly struct JoinAlliance : IEquatable<JoinAlliance>
    {
        [Key(0)] public readonly long AllianceId;

        [SerializationConstructor]
        public JoinAlliance(long allianceId)
        {
            AllianceId = allianceId;
        }

        public bool Equals(JoinAlliance other) => AllianceId == other.AllianceId;
        public override bool Equals(object obj) => obj is JoinAlliance other && Equals(other);
        public override int GetHashCode() => AllianceId.GetHashCode();
    }
}
