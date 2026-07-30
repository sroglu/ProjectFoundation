namespace PFound.GuidedOnboardingFlow.Core.Tests
{
    /// <summary>Entry point for the standalone PFound.GuidedOnboardingFlow.Core mono/csc test runner.</summary>
    internal static class Program
    {
        public static int Main()
        {
            RunnerTests.Run();
            AchievabilityTests.Run();
            TimeoutCancelTests.Run();
            ManagerTests.Run();
            ManagerCapabilitiesTests.Run();
            PersistenceTests.Run();
            BuiltInStepTests.Run();
            return TestKit.Summary("PFound.GuidedOnboardingFlow.Core");
        }
    }
}
