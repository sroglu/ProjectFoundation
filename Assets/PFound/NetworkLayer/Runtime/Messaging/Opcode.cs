using System;

namespace PFound.NetworkLayer
{
    /// <summary>
    /// Composes the flat wire <c>ushort</c> opcode from a banded two-level scheme: a high-byte
    /// DOMAIN (one subsystem) plus a low-byte OP (an operation local to that subsystem), so
    /// <c>opcode = (domain &lt;&lt; 8) | op</c>. The only globally-coordinated list is a tiny central
    /// domain enum; each domain then owns its own small op enum with local values (1..255). Different
    /// domain ⇒ different high byte ⇒ cross-subsystem collisions are structurally impossible, while the
    /// catalog's duplicate-enroll guard still backstops within-domain dupes.
    /// </summary>
    public static class Opcode
    {
        /// <summary>
        /// Fold a domain enum and an op enum into the banded wire opcode. Each part is narrowed to a
        /// single byte via <see cref="Convert.ToByte(object)"/>, which throws on overflow — a domain or
        /// op value that does not fit in a byte fails fast here rather than silently colliding on the wire.
        /// </summary>
        public static ushort Of(Enum domain, Enum op)
            => (ushort)((Convert.ToByte(domain) << 8) | Convert.ToByte(op));
    }
}
