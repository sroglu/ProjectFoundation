using System;
using MessagePack;

namespace PFound.NetworkLayer.Samples.Wallet
{
    /// <summary>
    /// The result the server returns for a spend: the wallet's balance after the deduction. Immutable
    /// and MessagePack-serialized, mirroring <see cref="SpendCoins"/> on the reply side.
    /// </summary>
    [MessagePackObject]
    public readonly struct SpendOutcome : IEquatable<SpendOutcome>
    {
        [Key(0)] public readonly long NewBalance;

        [SerializationConstructor]
        public SpendOutcome(long newBalance)
        {
            NewBalance = newBalance;
        }

        public bool Equals(SpendOutcome other) => NewBalance == other.NewBalance;
        public override bool Equals(object obj) => obj is SpendOutcome other && Equals(other);
        public override int GetHashCode() => NewBalance.GetHashCode();
    }
}
