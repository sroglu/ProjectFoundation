using MessagePack;

// .NET Standard 2.1 (Unity's api compatibility level) doesn't ship IsExternalInit, which the C# `init`
// accessor on the DTO properties needs to compile. Single-line shim — the same trick used elsewhere in the
// codebase for `init` properties.
namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }

namespace PFound.NetworkLayer.Samples
{
    /// <summary>
    /// The MessagePack source-generated resolver for this sample assembly's wire DTOs. MessagePack's official
    /// generator fills this <c>[GeneratedMessagePackResolver]</c> partial with a formatter for every
    /// <c>[MessagePackObject]</c> DTO declared here. A host pushes <see cref="Instance"/> into the body codec —
    /// see <c>GameMessages.CreateCodec</c>.
    /// </summary>
    [GeneratedMessagePackResolver]
    public partial class SampleNetworkResolver { }
}
