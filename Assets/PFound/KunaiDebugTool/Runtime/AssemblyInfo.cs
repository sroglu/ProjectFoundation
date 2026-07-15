using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("PFound.KunaiDebugTool.Tests")]

// Unity 6 / .NET Standard 2.1 doesn't ship IsExternalInit, which the C# 9
// `init` accessor needs to compile. Single-line shim — same trick used by
// every modern .NET-Standard codebase that wants `init` properties.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
