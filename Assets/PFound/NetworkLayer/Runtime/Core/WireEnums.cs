namespace PFound.NetworkLayer
{
    /// <summary>
    /// The three roles a framed message can play on the wire. Values are the
    /// literal first byte of every frame; chosen as a small non-zero set so a
    /// zeroed buffer never decodes as a valid kind.
    /// </summary>
    public enum MessageKind : byte
    {
        Request = 0x11,
        Reply   = 0x12,
        Notify  = 0x13,
    }

    /// <summary>
    /// Success/failure carried in a reply frame's header. Kept deliberately small
    /// (fits one byte) and app-neutral; game code layers its own domain errors on
    /// top inside the reply payload.
    /// </summary>
    public enum ReplyStatus : byte
    {
        Ok         = 0,
        Faulted    = 1,
        Expired    = 2,
        Refused    = 3,
        Unroutable = 4,
    }
}
