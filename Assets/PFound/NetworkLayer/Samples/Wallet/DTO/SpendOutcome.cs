using MessagePack;

namespace PFound.NetworkLayer.Samples.Wallet
{
    /// <summary>
    /// The result the server returns for a spend: the wallet's balance after the deduction. Immutable and
    /// MessagePack-serialized, mirroring <see cref="SpendCoins"/> on the reply side.
    /// </summary>
    [MessagePackObject]
    public readonly partial struct SpendOutcome
    {
        [Key(0)] public long NewBalance { get; init; }
    }
}
