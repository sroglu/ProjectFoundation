using PFound.NetworkLayer;

namespace PFound.NetworkLayer.CodegenSample
{
    /// <summary>Demo band list for the codegen sample (its own domain enum, isolated from other samples).</summary>
    public enum SampleDomain : byte
    {
        Wallet = 7,
    }

    /// <summary>Demo op list for the sample Wallet domain. Requests and notifies share the 1..255 op space.</summary>
    public enum SampleWalletOp : byte
    {
        Spend = 1,
        BalanceChanged = 3,
    }

    /// <summary>
    /// One compact RPC declaration — the source generator turns this into <c>Spend.Req</c>, <c>Spend.Reply</c>,
    /// <c>Spend.RequestMessage</c>, <c>Spend.ReplyMessage</c>, and <c>Spend.Register</c>. Fields are declared
    /// out of index order on purpose: the wire key follows the explicit index, never declaration order.
    /// </summary>
    [NetworkOp(SampleDomain.Wallet, SampleWalletOp.Spend)]
    public partial class Spend
    {
        [Request(1)] public long AccountId;
        [Request(0)] public int Amount;
        [Reply(0)] public long NewBalance;
    }

    /// <summary>One compact notify declaration — generates <c>BalanceChanged.Data</c>, its envelope, and enrolment.</summary>
    [NetworkNotify(SampleDomain.Wallet, SampleWalletOp.BalanceChanged)]
    public partial class BalanceChanged
    {
        [Field(0)] public long NewBalance;
    }
}
