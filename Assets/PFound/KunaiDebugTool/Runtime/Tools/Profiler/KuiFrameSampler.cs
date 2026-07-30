using System;
using Unity.Collections;

namespace Kunai
{
    /// <summary>
    /// Snapshot of recent perf stats over a rolling window. Cheap value type
    /// so the window can compute and forget without GC churn.
    /// </summary>
    internal struct KuiProfilerStats
    {
        public float AvgFps;     // 1000 / AvgMs
        public float MinFps;     // 1000 / PeakMs  — the worst-frame FPS (bottleneck)
        public float AvgMs;
        public float MinMs;      // best (lowest) per-tick avg in window
        public float PeakMs;     // worst (highest) per-tick peak in window
        public float AvgGcKb;
        public int   SampleCount;
    }

    /// <summary>
    /// Tick-aggregated frame sampler. Per-frame data accumulates into a
    /// pending bucket via <see cref="AccumulateFrame"/>; <see cref="CommitTick"/>
    /// flushes the bucket as a single ring entry (avg + peak + avg GC over
    /// the bucket). Both the graph and the readouts pull from the ring, so
    /// dropping the commit rate slows BOTH the visualisation and the text
    /// equally — the user controls the live update rate via one knob.
    ///
    /// <para>
    /// Backward-compatible <see cref="Sample"/> remains as
    /// <c>AccumulateFrame + CommitTick</c> so existing tests (one frame =
    /// one ring entry) keep working unchanged.
    /// </para>
    /// </summary>
    internal struct KuiFrameSampler : IDisposable
    {
        // Per-tick AVG ms across the bucket (kept name FrameTimesMs for
        // existing test access and to avoid touching the API surface).
        public NativeArray<float> FrameTimesMs;
        // Per-tick PEAK ms across the bucket — the worst frame seen since
        // the previous commit. Lets the graph plot "spikes between ticks"
        // instead of losing them to averaging.
        public NativeArray<float> FramePeakMs;
        // Per-tick AVG GC delta (KB).
        public NativeArray<float> GcDeltasKb;

        public int   Capacity;
        public int   Head;
        public int   Count;

        public float LastSmoothedFps;
        public float LastSmoothedFrameMs;
        public float LastSmoothedGcKb;
        public long  LastTotalMemoryBytes;

        // Pending bucket — frames seen since the last CommitTick.
        public float PendingSumMs;
        public float PendingPeakMs;
        public float PendingSumGcKb;
        public int   PendingFrameCount;

        public bool IsCreated => FrameTimesMs.IsCreated;

        const int SmoothWindow = 8;

        public static KuiFrameSampler Create(int capacity = 240)
        {
            return new KuiFrameSampler
            {
                Capacity = capacity > 0 ? capacity : 1,
                Head = 0,
                Count = 0,
                FrameTimesMs = new NativeArray<float>(capacity, Allocator.Persistent),
                FramePeakMs  = new NativeArray<float>(capacity, Allocator.Persistent),
                GcDeltasKb   = new NativeArray<float>(capacity, Allocator.Persistent),
                LastTotalMemoryBytes = GC.GetTotalMemory(false),
            };
        }

        public void Dispose()
        {
            if (FrameTimesMs.IsCreated) FrameTimesMs.Dispose();
            if (FramePeakMs.IsCreated)  FramePeakMs.Dispose();
            if (GcDeltasKb.IsCreated)   GcDeltasKb.Dispose();
        }

        /// <summary>
        /// Add one frame's data to the pending bucket. Cheap (no ring write,
        /// no GC). Call once per frame regardless of refresh rate.
        /// </summary>
        public void AccumulateFrame(float deltaTimeSec)
        {
            float ms = deltaTimeSec > 0f ? deltaTimeSec * 1000f : 0f;

            long now = GC.GetTotalMemory(forceFullCollection: false);
            long deltaBytes = now - LastTotalMemoryBytes;
            float deltaKb = deltaBytes > 0 ? deltaBytes / 1024f : 0f;
            LastTotalMemoryBytes = now;

            PendingSumMs   += ms;
            if (ms > PendingPeakMs) PendingPeakMs = ms;
            PendingSumGcKb += deltaKb;
            PendingFrameCount++;
        }

        /// <summary>
        /// Flush the pending bucket as one ring entry (avg + peak + avg GC).
        /// No-op when no frames have been accumulated since the last commit.
        /// </summary>
        public void CommitTick()
        {
            if (PendingFrameCount == 0) return;

            float invN = 1f / PendingFrameCount;
            FrameTimesMs[Head] = PendingSumMs * invN;
            FramePeakMs[Head]  = PendingPeakMs;
            GcDeltasKb[Head]   = PendingSumGcKb * invN;

            Head = (Head + 1) % Capacity;
            if (Count < Capacity) Count++;

            PendingSumMs = 0f;
            PendingPeakMs = 0f;
            PendingSumGcKb = 0f;
            PendingFrameCount = 0;

            // Refresh smoothed readouts over the last min(8, Count) ring
            // entries — used by tests + any caller that doesn't want to
            // call ComputeStats explicitly.
            int n = Count < SmoothWindow ? Count : SmoothWindow;
            float sumMs = 0f, sumKb = 0f;
            for (int i = 0; i < n; i++)
            {
                int idx = (Head - 1 - i + Capacity) % Capacity;
                sumMs += FrameTimesMs[idx];
                sumKb += GcDeltasKb[idx];
            }
            LastSmoothedFrameMs = n > 0 ? sumMs / n : 0f;
            LastSmoothedFps     = LastSmoothedFrameMs > 0.001f ? 1000f / LastSmoothedFrameMs : 0f;
            LastSmoothedGcKb    = n > 0 ? sumKb / n : 0f;
        }

        /// <summary>
        /// Backward-compatible single-shot sample: accumulates one frame
        /// and immediately commits it as one ring entry. Tests use this;
        /// production callers prefer <see cref="AccumulateFrame"/> +
        /// throttled <see cref="CommitTick"/> so the ring updates at a
        /// human-readable rate independent of game frame rate.
        /// </summary>
        public void Sample(float deltaTimeSec)
        {
            AccumulateFrame(deltaTimeSec);
            CommitTick();
        }

        /// <summary>
        /// Compute aggregate stats over the most recent <paramref name="window"/>
        /// ring entries (each a per-tick aggregate). Cheap — O(window).
        /// </summary>
        public KuiProfilerStats ComputeStats(int window)
        {
            int n = Count < window ? Count : window;
            if (n <= 0) return default;

            float sumMs = 0f, sumKb = 0f, peakMs = 0f, minMs = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                int idx = (Head - 1 - i + Capacity) % Capacity;
                float avgMs  = FrameTimesMs[idx];
                float pkMs   = FramePeakMs[idx];
                sumMs += avgMs;
                sumKb += GcDeltasKb[idx];
                if (pkMs   > peakMs) peakMs = pkMs;
                if (avgMs  < minMs)  minMs  = avgMs;
            }

            float avgMsAgg = sumMs / n;
            return new KuiProfilerStats
            {
                AvgMs       = avgMsAgg,
                MinMs       = minMs,
                PeakMs      = peakMs,
                AvgFps      = avgMsAgg > 0.001f ? 1000f / avgMsAgg : 0f,
                MinFps      = peakMs   > 0.001f ? 1000f / peakMs   : 0f,
                AvgGcKb     = sumKb / n,
                SampleCount = n,
            };
        }

        /// <summary>
        /// Copy ring contents into <paramref name="dst"/> in oldest-first
        /// order. Returns the number of valid samples written
        /// (≤ <paramref name="dst"/>.Length and ≤ <see cref="Count"/>).
        /// </summary>
        public int CopyOldestFirst(NativeArray<float> src, float[] dst)
        {
            int n = Count < dst.Length ? Count : dst.Length;
            int oldest = (Head - Count + Capacity) % Capacity;
            for (int i = 0; i < n; i++)
                dst[i] = src[(oldest + i) % Capacity];
            return n;
        }

        /// <summary>
        /// Copy the per-tick avg + peak series in oldest-first order into
        /// the supplied buffers. Each ring entry is already a per-tick
        /// aggregate, so callers can render the series directly without a
        /// secondary rolling window.
        /// </summary>
        public int CopyAvgPeakOldestFirst(float[] avgOut, float[] peakOut)
        {
            int len = avgOut.Length < peakOut.Length ? avgOut.Length : peakOut.Length;
            int n = Count < len ? Count : len;
            int oldest = (Head - Count + Capacity) % Capacity;
            for (int i = 0; i < n; i++)
            {
                int idx = (oldest + i) % Capacity;
                avgOut[i]  = FrameTimesMs[idx];
                peakOut[i] = FramePeakMs[idx];
            }
            return n;
        }

        /// <summary>
        /// Copy the most recent <paramref name="requestedCount"/> ring
        /// entries (avg + peak) in oldest-first order. Lets the graph fix a
        /// time window in seconds and adjust the displayed entry count to
        /// the refresh rate, keeping the visual scroll speed constant
        /// regardless of how many samples per second the user opted in to.
        /// </summary>
        public int CopyAvgPeakLastN(int requestedCount, float[] avgOut, float[] peakOut)
        {
            int len = avgOut.Length < peakOut.Length ? avgOut.Length : peakOut.Length;
            int n = requestedCount;
            if (n > Count) n = Count;
            if (n > len)   n = len;
            if (n <= 0) return 0;

            // Skip (Count - n) oldest entries; start from the (Count-n)-th
            // entry counted from the oldest.
            int from = (Head - n + Capacity) % Capacity;
            for (int i = 0; i < n; i++)
            {
                int idx = (from + i) % Capacity;
                avgOut[i]  = FrameTimesMs[idx];
                peakOut[i] = FramePeakMs[idx];
            }
            return n;
        }

        public float MaxFrameTimeMs()
        {
            float max = 0f;
            for (int i = 0; i < Count; i++)
            {
                int idx = (Head - 1 - i + Capacity) % Capacity;
                if (FrameTimesMs[idx] > max) max = FrameTimesMs[idx];
            }
            return max;
        }
    }
}
