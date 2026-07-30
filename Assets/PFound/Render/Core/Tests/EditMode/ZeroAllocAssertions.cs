using System;
using System.Collections;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;

namespace PFound.Render.Core.Tests
{
    /// <summary>
    /// Helpers for asserting that a code path produces zero managed allocations
    /// over a window of frames. Wraps <see cref="ProfilerRecorder"/> over the
    /// <c>GC.Alloc.Size</c> sample group in <see cref="ProfilerCategory.Memory"/>.
    /// </summary>
    /// <remarks>
    /// Call from PlayMode tests (use <c>[UnityTest]</c> + <c>IEnumerator</c>). Run
    /// the code path twice before measuring (warm-up) to discount JIT/cache costs.
    /// Sample window defaults to 60 frames per spec SC-004.
    /// </remarks>
    public static class ZeroAllocAssertions
    {
        /// <summary>
        /// Drives <paramref name="action"/> for <paramref name="frameCount"/> frames
        /// under a profiler recorder and asserts the total managed-allocation byte
        /// count is zero across the window. Performs two warm-up frames first.
        /// </summary>
        /// <param name="action">Code path to exercise once per frame.</param>
        /// <param name="frameCount">Sample window size in frames. Default 60.</param>
        /// <param name="label">Optional label included in assertion failure messages.</param>
        public static IEnumerator AssertZeroAlloc(
            Action action,
            int frameCount = 60,
            string label = null)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (frameCount <= 0) throw new ArgumentOutOfRangeException(nameof(frameCount));

            // Warm-up: discount first-call costs.
            action();
            yield return null;
            action();
            yield return null;

            using var recorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                "GC.Alloc.Size",
                capacity: frameCount,
                options: ProfilerRecorderOptions.SumAllSamplesInFrame);

            long total = 0;
            for (int i = 0; i < frameCount; i++)
            {
                long before = recorder.CurrentValue;
                action();
                yield return null;
                long after = recorder.CurrentValue;
                total += Math.Max(0, after - before);
            }

            string prefix = string.IsNullOrEmpty(label) ? "Zero-alloc assertion" : $"Zero-alloc assertion ({label})";
            Assert.That(total, Is.EqualTo(0L),
                $"{prefix} failed: {total} bytes allocated across {frameCount} frames.");
        }
    }
}
