namespace GameSpecific.Networking
{
    /// <summary>
    /// The one central band list for the whole game: one entry per subsystem, its value forming the HIGH
    /// byte of every opcode that subsystem owns. Keeping every domain in this single sheet makes the game's
    /// entire opcode space legible from one spot, and makes cross-subsystem collisions impossible by
    /// construction (a different domain is always a different high byte).
    /// <see cref="Invalid"/> is 0 — the default sentinel, so a zero-initialised opcode is never a live band.
    /// </summary>
    public enum NetDomain : byte
    {
        Invalid = 0,
        Wallet = 1,    // 0x01 -> opcodes 0x01__
        Alliance = 2,  // 0x02 -> opcodes 0x02__
        Player = 3,    // 0x03 -> opcodes 0x03__
        Auth = 4,      // 0x04 -> opcodes 0x04__
    }

    /// <summary>
    /// The Wallet subsystem's operations — the LOW byte of each opcode, folded against
    /// <see cref="NetDomain.Wallet"/> at declaration time. One value per operation: the request carries the
    /// opcode, the reply is opcode-less (decoded by correlation), so there is no separate reply op.
    /// </summary>
    public enum WalletOp : byte
    {
        Invalid = 0,
        Spend = 1,  // 0x01 -> opcode 0x0101
    }

    /// <summary>
    /// The Alliance subsystem's operations — the LOW byte of each opcode, folded against
    /// <see cref="NetDomain.Alliance"/> at declaration time. Same one-value-per-operation rule as
    /// <see cref="WalletOp"/>.
    /// </summary>
    public enum AllianceOp : byte
    {
        Invalid = 0,
        Join = 1,  // 0x01 -> opcode 0x0201
    }

    /// <summary>
    /// The Player subsystem's operations — the LOW byte of each opcode, folded against
    /// <see cref="NetDomain.Player"/> at declaration time. Same one-value-per-operation rule as
    /// <see cref="WalletOp"/>.
    /// </summary>
    public enum PlayerOp : byte
    {
        Invalid = 0,
        GetData = 1,  // 0x01 -> opcode 0x0301
        Presence = 2,  // 0x02 -> opcode 0x0302
    }

    /// <summary>
    /// The Auth subsystem's operations — the LOW byte of each opcode, folded against
    /// <see cref="NetDomain.Auth"/> at declaration time. Same one-value-per-operation rule as
    /// <see cref="WalletOp"/>.
    /// </summary>
    public enum AuthOp : byte
    {
        Invalid = 0,
        Login = 1,  // 0x01 -> opcode 0x0401
    }
}
