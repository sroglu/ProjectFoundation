using PFound.ServerOperationFlow.Core;

namespace GameSpecific.Networking
{
    /// <summary>
    /// Every outcome an operation can fail with in this game — the <c>TCode</c> carried by
    /// <see cref="ServerOperationResult{TCode}"/>, so a flow's <c>ResultCode</c> is this typed enum, not a bare int.
    /// The <see cref="OperationResultCodeAttribute"/> marker lets the "Create ServerOperation flow" quick-fix
    /// scaffold flows against <c>ServerOperationResult&lt;OpResult&gt;</c> (mark exactly one enum per assembly).
    /// <see cref="Invalid"/> = 0 is the uninitialized sentinel — never a real outcome. A failure surfaces as the
    /// toast <c>"[OpResult.&lt;Name&gt;] &lt;diagnostic&gt;"</c> (see the code-toast presenter).
    /// </summary>
    [OperationResultCode]
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
