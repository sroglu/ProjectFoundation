using PFound.Backend.Core;

internal static class Program
{
    static int Main()
    {
        CoreTests.Run();
        return TestKit.Summary("PFound.Backend.Core");
    }
}
