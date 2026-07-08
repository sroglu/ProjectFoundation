// Standalone mono/csc test runner for the engine-free client latency telemetry.
//
// The full NetworkLayer test suite (Tests/EditAndPlayModes) runs under Unity/NUnit
// and pulls in MessagePack facades a bare csc lacks. The per-opcode latency logic,
// however, holds no engine types, so it is verified here with plain csc/mono:
//
//   csc -nologo -warn:0 -define:PF_STANDALONE_TESTS -out:/tmp/pf_net.exe \
//       Assets/PFound/NetworkLayer/Runtime/Diagnostics/CallLatencyStats.cs \
//       Assets/PFound/NetworkLayer/Runtime/Messaging/OutstandingCalls.cs \
//       Assets/PFound/NetworkLayer/Runtime/Messaging/Message.cs \
//       Assets/PFound/NetworkLayer/Runtime/Core/NetworkExceptions.cs \
//       Assets/PFound/NetworkLayer/Tests/Standalone/CallLatencyStandaloneTests.cs \
//   && mono /tmp/pf_net.exe

// PF_STANDALONE_TESTS is never defined by Unity, so this runner (with its own
// Main) is skipped by the editor and only compiles under the explicit csc build
// above, which defines it. That keeps the entry point out of Assembly-CSharp.
#if PF_STANDALONE_TESTS
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PFound.NetworkLayer;

static class CallLatencyStandaloneTests
{
    static int _passed;
    static int _failed;

    static void Check(bool ok, string what)
    {
        if (ok) { _passed++; return; }
        _failed++;
        Console.WriteLine("FAIL: " + what);
    }

    static void PerOpcodeAverageMinMaxCount()
    {
        var stats = new CallLatencyStats();
        stats.RecordRoundTrip(10, 100);
        stats.RecordRoundTrip(10, 50);
        stats.RecordRoundTrip(10, 300);
        stats.RecordRoundTrip(20, 8); // different opcode, isolated

        CallLatencyStats.OpcodeLatency a;
        Check(stats.TryGet(10, out a), "opcode 10 present");
        Check(a.Completed == 3, "opcode 10 count == 3");
        Check(a.MinMs == 50, "opcode 10 min == 50");
        Check(a.MaxMs == 300, "opcode 10 max == 300");
        Check(a.SumMs == 450, "opcode 10 sum == 450");
        Check(Math.Abs(a.AverageMs - 150.0) < 1e-9, "opcode 10 average == 150");

        CallLatencyStats.OpcodeLatency b;
        Check(stats.TryGet(20, out b), "opcode 20 present");
        Check(b.Completed == 1 && b.MinMs == 8 && b.MaxMs == 8, "opcode 20 isolated single sample");

        Check(stats.PerOpcode.Count == 2, "two opcodes tracked");
    }

    static void AverageOfEmptyOpcodeIsZero()
    {
        var stat = new CallLatencyStats.OpcodeLatency();
        Check(Math.Abs(stat.AverageMs - 0.0) < 1e-9, "average of zero-count record is 0, no divide-by-zero");
    }

    static void SlowCallFiresAtOrAboveThreshold()
    {
        var fired = new List<CallLatencyStats.SlowCallReport>();
        var stats = new CallLatencyStats(200);
        stats.SlowCall += r => fired.Add(r);

        stats.RecordRoundTrip(7, 199); // below -> quiet
        stats.RecordRoundTrip(7, 200); // exactly at threshold -> fires
        stats.RecordRoundTrip(7, 640); // above -> fires

        Check(fired.Count == 2, "slow-call fires at and above threshold, not below");
        Check(stats.SlowCallCount == 2, "slow-call count == 2");
        Check(fired[0].Opcode == 7 && fired[0].RoundTripMs == 200 && fired[0].ThresholdMs == 200,
            "first slow-call report carries opcode/round-trip/threshold");
        Check(fired[1].RoundTripMs == 640, "second slow-call report carries the actual round-trip");
    }

    static void ThresholdOffStillTalliesLatency()
    {
        var fired = 0;
        var stats = new CallLatencyStats(0); // off
        stats.SlowCall += _ => fired++;
        stats.RecordRoundTrip(3, 9000);

        Check(fired == 0, "no slow-call event when threshold is off");
        CallLatencyStats.OpcodeLatency s;
        Check(stats.TryGet(3, out s) && s.Completed == 1 && s.MaxMs == 9000,
            "latency still tallied with the event off");
    }

    static void ResetClearsEverything()
    {
        var stats = new CallLatencyStats(10);
        stats.RecordRoundTrip(1, 500);
        stats.Reset();
        Check(stats.PerOpcode.Count == 0, "reset clears per-opcode table");
        Check(stats.SlowCallCount == 0, "reset clears slow-call count");
    }

    // OutstandingCalls.Closed must fire for every terminal outcome so the peer can
    // close its round-trip timer no matter how the call ends.
    static void OutstandingClosedFiresOnEveryOutcome()
    {
        var closed = new List<uint>();
        var calls = new OutstandingCalls();
        calls.Closed += closed.Add;

        // settle
        var p1 = new TaskCompletionSource<Message>();
        calls.Open(1, p1, 10_000);
        Check(calls.Settle(1, null), "settle known token");

        // break (fault)
        var p2 = new TaskCompletionSource<Message>();
        calls.Open(2, p2, 10_000);
        Check(calls.Break(2, new CallExpiredFault()), "break known token");

        // expire
        var p3 = new TaskCompletionSource<Message>();
        calls.Open(3, p3, 100);
        calls.ExpireDue(200);

        // break-all (link drop)
        var p4 = new TaskCompletionSource<Message>();
        calls.Open(4, p4, 10_000);
        calls.BreakAll(new CallExpiredFault());

        Check(closed.Contains(1), "Closed fired on settle");
        Check(closed.Contains(2), "Closed fired on break");
        Check(closed.Contains(3), "Closed fired on expire");
        Check(closed.Contains(4), "Closed fired on break-all");
        Check(closed.Count == 4, "exactly four closes, one per call");
        Check(calls.Count == 0, "table empty after all outcomes");

        // Unknown token: no Closed, no double count.
        closed.Clear();
        Check(!calls.Settle(999, null), "settle unknown token returns false");
        Check(closed.Count == 0, "Closed does not fire for an unknown token");
    }

    static int Main()
    {
        PerOpcodeAverageMinMaxCount();
        AverageOfEmptyOpcodeIsZero();
        SlowCallFiresAtOrAboveThreshold();
        ThresholdOffStillTalliesLatency();
        ResetClearsEverything();
        OutstandingClosedFiresOnEveryOutcome();

        Console.WriteLine($"CallLatency standalone: {_passed} passed, {_failed} failed.");
        return _failed == 0 ? 0 : 1;
    }
}
#endif
