using System;
using MessagePack;

namespace PFound.NetworkLayer.Samples.Wallet
{
    /// <summary>
    /// The payload a client sends to spend coins: an immutable, MessagePack-serialized value carrying
    /// just the amount to deduct. Single-keyed — the simplest shape a content DTO takes.
    /// </summary>
    [MessagePackObject]
    public readonly struct SpendCoins : IEquatable<SpendCoins>
    {
        [Key(0)] public readonly int Amount;

        [SerializationConstructor]
        public SpendCoins(int amount)
        {
            Amount = amount;
        }

        public bool Equals(SpendCoins other) => Amount == other.Amount;
        public override bool Equals(object obj) => obj is SpendCoins other && Equals(other);
        public override int GetHashCode() => Amount;
    }
}
