// Standalone entry point for the engine-free Pause test suite. Exit code 0 = all green.
internal static class Program
{
    public static int Main()
    {
        PauseCoordinatorTests.Run();
        return TestKit.Summary("PFound.Utilities.Pause");
    }
}
