using System.Collections.Generic;
using PFound.Utilities.Pause;

// Behavior tests for the reference-counted pause coordinator: edge-only notifications,
// nested acquire/release counting, idempotent double-release, and bulk clear.
internal static class PauseCoordinatorTests
{
    public static void Run()
    {
        StartsRunning();
        FirstAcquirePausesAndFiresOnce();
        NestedAcquiresDoNotRefire();
        ResumesOnlyWhenLastReleased();
        DoubleReleaseIsIdempotent();
        DisposeReleases();
        ReleaseAllResumesOnce();
        ReleaseAllWhileRunningIsNoOp();
        ReleaseAfterReleaseAllIsNoOp();
        ReasonsAreTracked();
        HandleReflectsHeldState();
    }

    private static int _edges;
    private static bool _lastState;

    private static PauseCoordinator Fresh()
    {
        _edges = 0;
        _lastState = false;
        var c = new PauseCoordinator();
        c.PauseChanged += s => { _edges++; _lastState = s; };
        return c;
    }

    private static void StartsRunning()
    {
        var c = new PauseCoordinator();
        TestKit.Check(!c.IsPaused, "fresh coordinator is running");
        TestKit.AreEqual(0, c.ActiveCount, "fresh coordinator has no handles");
    }

    private static void FirstAcquirePausesAndFiresOnce()
    {
        var c = Fresh();
        var h = c.Acquire();
        TestKit.Check(c.IsPaused, "acquire pauses");
        TestKit.AreEqual(1, _edges, "first acquire fires exactly one edge");
        TestKit.Check(_lastState, "edge reports paused=true");
        h.Release();
    }

    private static void NestedAcquiresDoNotRefire()
    {
        var c = Fresh();
        var a = c.Acquire();
        var b = c.Acquire();
        var cc = c.Acquire();
        TestKit.AreEqual(3, c.ActiveCount, "three handles held");
        TestKit.AreEqual(1, _edges, "only the 0->1 transition fires");
        a.Release();
        b.Release();
        cc.Release();
    }

    private static void ResumesOnlyWhenLastReleased()
    {
        var c = Fresh();
        var a = c.Acquire();
        var b = c.Acquire();
        a.Release();
        TestKit.Check(c.IsPaused, "still paused with one handle left");
        TestKit.AreEqual(1, _edges, "no resume edge yet");
        b.Release();
        TestKit.Check(!c.IsPaused, "resumed after last release");
        TestKit.AreEqual(2, _edges, "resume fires the second edge");
        TestKit.Check(!_lastState, "edge reports paused=false");
    }

    private static void DoubleReleaseIsIdempotent()
    {
        var c = Fresh();
        var h = c.Acquire();
        h.Release();
        h.Release();
        h.Release();
        TestKit.AreEqual(2, _edges, "double release does not fire extra edges");
        TestKit.AreEqual(0, c.ActiveCount, "count stays at zero");
        TestKit.Check(!c.IsPaused, "still running after redundant releases");
    }

    private static void DisposeReleases()
    {
        var c = Fresh();
        using (c.Acquire("scoped"))
        {
            TestKit.Check(c.IsPaused, "paused inside using block");
        }
        TestKit.Check(!c.IsPaused, "resumed after using block disposes handle");
        TestKit.AreEqual(2, _edges, "dispose produced the resume edge");
    }

    private static void ReleaseAllResumesOnce()
    {
        var c = Fresh();
        c.Acquire();
        c.Acquire();
        c.Acquire();
        c.ReleaseAll();
        TestKit.Check(!c.IsPaused, "release-all resumes");
        TestKit.AreEqual(0, c.ActiveCount, "release-all clears all handles");
        TestKit.AreEqual(2, _edges, "release-all fires a single resume edge");
    }

    private static void ReleaseAllWhileRunningIsNoOp()
    {
        var c = Fresh();
        c.ReleaseAll();
        TestKit.AreEqual(0, _edges, "release-all while running fires nothing");
        TestKit.Check(!c.IsPaused, "still running");
    }

    private static void ReleaseAfterReleaseAllIsNoOp()
    {
        var c = Fresh();
        var h = c.Acquire();
        c.ReleaseAll();
        h.Release();
        TestKit.AreEqual(2, _edges, "releasing a handle already cleared by release-all is inert");
        TestKit.Check(!c.IsPaused, "coordinator remains running");
    }

    private static void ReasonsAreTracked()
    {
        var c = new PauseCoordinator();
        c.Acquire("loading");
        c.Acquire();
        c.Acquire("dialog");
        IReadOnlyList<string> reasons = c.ActiveReasons;
        TestKit.AreEqual(3, reasons.Count, "three reasons reported");
        TestKit.Check(reasons[0] == "loading", "reason order preserved (first)");
        TestKit.Check(reasons[1] == "", "missing reason surfaces as empty string");
        TestKit.Check(reasons[2] == "dialog", "reason order preserved (last)");
    }

    private static void HandleReflectsHeldState()
    {
        var c = new PauseCoordinator();
        var h = c.Acquire();
        TestKit.Check(h.IsHeld, "handle held after acquire");
        h.Release();
        TestKit.Check(!h.IsHeld, "handle not held after release");
    }
}
