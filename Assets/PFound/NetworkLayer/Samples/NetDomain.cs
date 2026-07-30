namespace PFound.NetworkLayer.Samples
{
    /// <summary>
    /// The central band list: one entry per subsystem, its value forming the HIGH byte of every
    /// opcode that subsystem owns. Keeping it in a single file makes the whole game's opcode space
    /// legible from one spot; cross-subsystem collisions are impossible by construction (different
    /// domain ⇒ different high byte).
    /// </summary>
    public enum NetDomain : byte
    {
        Invalid = 0,
        Wallet = 1,    // 0x01 -> opcodes 0x01__
        Alliance = 2,  // 0x02 -> opcodes 0x02__
    }
}
