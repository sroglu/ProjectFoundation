// Single entry point for the standalone PFound.Compression test runner.
// Exit code 0 = all green.
internal static class Program
{
    public static int Main()
    {
        LzmaTests.Run();
        return TestKit.Summary("PFound.Compression");
    }
}
