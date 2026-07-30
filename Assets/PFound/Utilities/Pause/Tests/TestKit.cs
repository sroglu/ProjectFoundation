using System;

// Minimal standalone assertion helper for the engine-free Pause suite. Avoids any NUnit or
// UnityEngine dependency so the counting logic can be compiled and executed with csc/mono.
internal static class TestKit
{
    public static int Passed;
    public static int Failed;

    public static void Check(bool condition, string name)
    {
        if (condition) { Passed++; }
        else { Failed++; Console.WriteLine("  FAIL: " + name); }
    }

    public static void AreEqual(int expected, int actual, string name)
    {
        Check(expected == actual, name + " (expected " + expected + ", got " + actual + ")");
    }

    public static int Summary(string label)
    {
        Console.WriteLine("--------------------------------------------------");
        Console.WriteLine(label + ": passed=" + Passed + " failed=" + Failed);
        return Failed == 0 ? 0 : 1;
    }
}
