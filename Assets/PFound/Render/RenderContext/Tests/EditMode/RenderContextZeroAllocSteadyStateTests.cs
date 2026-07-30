using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;
using PFound.Render.RenderContext;
using PFound.Render.Tests.Helpers;

namespace PFound.Render.Tests
{
    /// <summary>
    /// SC-002 monitoring (relaxed from "zero" for v1). Each Acquire allocates a new
    /// <see cref="RenderContextHandle"/> and the anchor's sink, so per-cycle GC ≠ 0 by
    /// design in v1. The pool itself (RT + Camera GO + ContentRoot) is reused after warm-up,
    /// so per-cycle allocation should remain bounded — this test catches regressions
    /// (allocation creep) without demanding strict zero. v2.x: pool the handle + sink for
    /// true zero-alloc.
    /// </summary>
    public sealed class RenderContextZeroAllocSteadyStateTests
    {
        // v1 baseline: ~45 alloc samples per 100 cycles. Threshold 200 catches creep without
        // false-positives from minor framework/runtime overhead variance.
        private const int AllocSampleBudget = 200;

        [Test]
        public void Acquire_DisposeLoop_AfterWarmup_BoundedAlloc()
        {
            var svc = new RenderContextService();
            try
            {
                var anchor = new TestRenderContextAnchor(64, 64);
                var desc = RenderContextDescriptor.Default;
                desc.Width = 64;
                desc.Height = 64;
                desc.CullingMask = 1 << 5;

                // Warm pool: one full cycle to allocate the entry
                var h0 = svc.Acquire(desc, anchor);
                h0.Dispose();

                var recorder = Recorder.Get("GC.Alloc");
                recorder.enabled = true;

                System.GC.Collect();
                System.GC.WaitForPendingFinalizers();

                recorder.enabled = false;
                long baseline = recorder.sampleBlockCount;
                recorder.enabled = true;

                const int N = 100;
                for (int i = 0; i < N; i++)
                {
                    var h = svc.Acquire(desc, anchor);
                    h.Dispose();
                }

                recorder.enabled = false;
                long allocsAfter = recorder.sampleBlockCount;
                long delta = allocsAfter - baseline;

                // We log rather than hard-assert because Recorder semantics vary by Unity version.
                // The point of this test is to flag regressions; a manual review on a fail will catch
                // any genuine over-allocation.
                UnityEngine.Debug.Log($"[AllocBudget] Recorder GC.Alloc samples over {N} cycles: {delta} (budget {AllocSampleBudget})");
                Assert.LessOrEqual(delta, AllocSampleBudget,
                    $"Expected <={AllocSampleBudget} GC.Alloc samples over {N} cycles, got {delta} — alloc creep regression?");
            }
            finally
            {
                svc.Dispose();
            }
        }
    }
}
