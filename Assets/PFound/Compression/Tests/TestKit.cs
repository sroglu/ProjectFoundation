using System;

// Tiny standalone assertion kit for the PFound.Compression suite. No NUnit dependency, so the
// engine-free codec compiles + runs under mono/csc without Unity. Exit code 0 = all green.
internal static class TestKit
{
    public static int Passed;
    public static int Failed;

    public static void Check(bool cond, string name)
    {
        if (cond) { Passed++; }
        else { Failed++; Console.WriteLine("  FAIL: " + name); }
    }

    public static void Throws<TEx>(Action a, string name) where TEx : Exception
    {
        try { a(); Failed++; Console.WriteLine("  FAIL (no throw): " + name); }
        catch (TEx) { Passed++; }
        catch (Exception e) { Failed++; Console.WriteLine("  FAIL (wrong ex " + e.GetType().Name + "): " + name); }
    }

    public static int Summary(string label)
    {
        Console.WriteLine("--------------------------------------------------");
        Console.WriteLine(label + ": passed=" + Passed + " failed=" + Failed);
        return Failed == 0 ? 0 : 1;
    }
}
