namespace GameSpecific.Networking
{
    /// <summary>
    /// Every user-facing outcome an operation can fail with in this game. Each named value maps BY CONVENTION to
    /// the localization key <c>"op.result.&lt;Name&gt;"</c>, so the localized toast comes from the enum member name
    /// alone — add a value here, add the matching localization row, done. <see cref="Invalid"/> = 0 is the
    /// uninitialized sentinel: it is never a real outcome and falls back to the generic toast.
    /// </summary>
    public enum OpResult
    {
        Invalid = 0,
        AmountNotPositive = 1,
        InsufficientBalance = 2,
        AllianceIdMissing = 3,
        AlreadyInAlliance = 4,
        ServerFaulted = 5,
        ServerRefused = 6,
        RequestExpired = 7,
        Unroutable = 8,
    }
}
