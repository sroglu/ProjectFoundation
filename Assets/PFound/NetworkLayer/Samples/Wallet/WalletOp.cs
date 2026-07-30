namespace PFound.NetworkLayer.Samples.Wallet
{
    /// <summary>
    /// The Wallet subsystem's operations — the LOW byte of each opcode, folded against
    /// <see cref="NetDomain.Wallet"/> at enrol time. One op per operation: the request carries the value,
    /// the reply is opcode-less (decoded by correlation), so there is no separate reply op.
    /// </summary>
    public enum WalletOp : byte
    {
        Invalid = 0,
        Spend = 1,  // 0x01 -> opcode 0x0101
    }
}
