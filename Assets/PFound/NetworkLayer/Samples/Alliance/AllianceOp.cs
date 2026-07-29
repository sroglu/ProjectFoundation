namespace PFound.NetworkLayer.Samples.Alliance
{
    /// <summary>
    /// The Alliance subsystem's operations — the LOW byte of each opcode, folded against
    /// <see cref="NetDomain.Alliance"/> at enrol time. A request and its reply are distinct catalog
    /// entries, so each carries its own value.
    /// </summary>
    public enum AllianceOp : byte
    {
        Join = 1,       // 0x01 -> opcode 0x0201
        JoinReply = 2,  // 0x02 -> opcode 0x0202
    }
}
