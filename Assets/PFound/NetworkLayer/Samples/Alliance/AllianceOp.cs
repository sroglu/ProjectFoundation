namespace PFound.NetworkLayer.Samples.Alliance
{
    /// <summary>
    /// The Alliance subsystem's operations — the LOW byte of each opcode, folded against
    /// <see cref="NetDomain.Alliance"/> at enrol time. One op per operation: the request carries the value,
    /// the reply is opcode-less (decoded by correlation), so there is no separate reply op.
    /// </summary>
    public enum AllianceOp : byte
    {
        Invalid = 0,
        Join = 1,  // 0x01 -> opcode 0x0201
    }
}
