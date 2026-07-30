// Single entry point for the standalone PFound.ECS test runner. Each suite exposes a
// static Run() and shares the TestKit counters; exit code 0 = all green.
internal static class Program
{
    public static int Main()
    {
        CoreCharacterizationTests.Run();
        QueryCharacterizationTests.Run();
        CommandBufferTests.Run();
        EventTests.Run();
        InteractionTests.Run();
        DataTests.Run();
        SystemTests.Run();
        SystemRegistryTests.Run();
        return TestKit.Summary("PFound.ECS");
    }
}
