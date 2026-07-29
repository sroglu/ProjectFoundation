namespace PFound.NetworkLayer.Samples.Wallet
{
    /// <summary>
    /// The Wallet subsystem's operations — the LOW byte of each opcode, folded against
    /// <see cref="NetDomain.Wallet"/> at enrol time. A request and its reply are distinct catalog
    /// entries, so each carries its own value.
    /// </summary>
    public enum WalletOp : byte
    {
        Spend = 1,       // 0x01 -> opcode 0x0101
        SpendReply = 2,  // 0x02 -> opcode 0x0102
    }
}
