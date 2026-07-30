using MessagePack;

// .NET Standard 2.1 (Unity's api compatibility level) doesn't ship IsExternalInit, which the C# `init`
// accessor on the DTO properties needs to compile. Single-line shim — the same trick used elsewhere in the
// codebase (see KunaiDebugTool) for `init` properties.
namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }

namespace GameSpecific.Networking
{
    /// <summary>
    /// The one MessagePack source-generated resolver for this assembly's wire DTOs. MessagePack's official
    /// generator fills this partial (the <c>[GeneratedMessagePackResolver]</c> anchor) with a formatter for
    /// every <c>[MessagePackObject]</c> DTO declared here. The host pushes <see cref="Instance"/> into the
    /// body codec at boot (see <c>GameNetworkSetup</c>) — no reflection discovery, AOT-safe.
    /// </summary>
    [GeneratedMessagePackResolver]
    public partial class GameNetworkResolver { }
}
