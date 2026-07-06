using Unity.Mathematics;
using UnityEngine;

namespace Kunai
{
    /// <summary>
    /// Two-line FPS history graph: avg FPS (smooth top line) and min FPS
    /// (bottleneck — drops on hitches). Reads tick-aggregated avg ms +
    /// peak ms from the sampler ring and converts to FPS for display.
    /// One <see cref="KuiCommandBuffer.PushLine"/> per adjacent sample
    /// pair → same atlas / shader / draw call as every other Kunai widget.
    /// Cap line at 60 FPS marks the budget reference. Y-axis auto-scales
    /// to <c>max(avg FPS) * 1.1</c>, never below the cap.
    ///
    /// <para>
    /// Layout: a steady-state graph at the current Hz fills the full graph
    /// width (pixelPerSample = totalWidth ÷ (Hz × displaySec)). So at low
    /// Hz each sample is fat (wide spacing); at high Hz each sample is
    /// narrow. During startup, when the ring has fewer entries than
    /// expected, the drawn area is right-aligned within the rect — the
    /// existing samples don't get stretched to fill the empty left side.
    /// </para>
    /// </summary>
    internal static class KuiFrameGraphRenderer
    {
        // Reused per-frame so we don't allocate managed arrays each render.
        // Sized to match KuiFrameSampler's default capacity (240).
        static readonly float[] s_avgMs   = new float[240];
        static readonly float[] s_peakMs  = new float[240];
        static readonly float[] s_avgFps  = new float[240];
        static readonly float[] s_minFps  = new float[240];

        // Y-axis auto-scale references the most recent ~1s of ticks
        // (computed from the active commit Hz) so stale hitches don't
        // compress live data.
        const float ScaleWindowSec = 1f;

        /// <summary>
        /// Render the graph.
        /// </summary>
        /// <param name="hz">Current commit Hz (= scroll cadence).</param>
        /// <param name="displaySec">Time window the graph represents at any Hz.</param>
        public static void Render(KuiFrameSampler sampler, float4 graphRect,
                                  float capLineFps, KuiContext ctx,
                                  int hz, float displaySec)
        {
            // Effective clip = intersection of (graph rect, parent window clip).
            // Off-scale spikes snap at the graph top edge; the graph rect
            // itself is also clipped to the window so it can't overflow.
            float4 clip = IntersectClip(graphRect, ctx.Layout.CurrentClip);

            ctx.CommandBuffer.PushRect(graphRect, KuiStyles.SliderTrack, clip);

            // expectedSamples == samples that make a full graph at this Hz.
            // pixelPerSample sized so a full ring at the current Hz fills the
            // rect end-to-end: N samples form (N-1) line segments, so the
            // step is totalWidth / (N-1). At expected==1 we fall back to
            // totalWidth (single-dot path in DrawSeries handles n==1).
            int   expectedSamples = math.max(1, (int)math.round(hz * displaySec));
            float pixelPerSample  = expectedSamples > 1
                ? graphRect.z / (expectedSamples - 1)
                : graphRect.z;

            int n = sampler.CopyAvgPeakLastN(expectedSamples, s_avgMs, s_peakMs);
            if (n <= 0) return;

            // Convert ms → FPS for display:
            //   avg ms in tick      → avg FPS = 1000 / avg_ms     (top line)
            //   peak ms in tick     → min FPS = 1000 / peak_ms    (bottleneck line)
            // Since peak_ms ≥ avg_ms, min FPS ≤ avg FPS — bottleneck sits below.
            for (int i = 0; i < n; i++)
            {
                float am = s_avgMs[i];
                float pm = s_peakMs[i];
                s_avgFps[i] = am > 0.001f ? 1000f / am : 0f;
                s_minFps[i] = pm > 0.001f ? 1000f / pm : 0f;
            }

            // Y-axis: max of recent avg FPS × 1.1, never below capLineFps.
            int recent = math.min(math.max(1, (int)math.round(ScaleWindowSec * hz)), n);
            float maxFps = capLineFps;
            for (int i = n - recent; i < n; i++)
                if (s_avgFps[i] > maxFps) maxFps = s_avgFps[i];
            maxFps *= 1.1f;
            if (maxFps <= 0.001f) maxFps = capLineFps;

            // Right-align the drawn samples within the graph rect. The
            // newest sample sits at the rect's right edge; older samples
            // extend leftward by pixelPerSample each. Empty space stays on
            // the LEFT — never stretches a few samples to fill the area.
            float rightX = graphRect.x + graphRect.z;
            float xOrigin = rightX - (n - 1) * pixelPerSample;

            // Draw min-FPS first (under) so the avg line sits visually on top
            // when both are close. Min in red-orange (bottleneck), avg in blue.
            DrawSeries(s_minFps, n, xOrigin, pixelPerSample, graphRect, maxFps, KuiBottleneckColor, ctx, clip);
            DrawSeries(s_avgFps, n, xOrigin, pixelPerSample, graphRect, maxFps, KuiAvgColor,        ctx, clip);

            // Cap line: thin horizontal strip at the 60 FPS budget.
            float capY = graphRect.y + graphRect.w - (capLineFps / maxFps) * graphRect.w;
            ctx.CommandBuffer.PushRect(
                new float4(graphRect.x, capY, graphRect.z, math.max(1f, KuiDPI.Px(1f))),
                KuiCapLineColor, clip);
        }

        // AABB intersection of two (x, y, w, h) clip rects.
        static float4 IntersectClip(float4 a, float4 b)
        {
            float xMin = math.max(a.x, b.x);
            float yMin = math.max(a.y, b.y);
            float xMax = math.min(a.x + a.z, b.x + b.z);
            float yMax = math.min(a.y + a.w, b.y + b.w);
            return new float4(xMin, yMin, math.max(0f, xMax - xMin), math.max(0f, yMax - yMin));
        }

        // Connect adjacent samples with a diagonal oriented quad. Single dot
        // for n==1 (first frame after open). One PushLine per segment — all
        // four vertices share the same atlas and shader as every Rect, so
        // the single-draw-call invariant is preserved.
        static void DrawSeries(float[] data, int n, float xOrigin, float colW,
                               float4 rect, float maxValue,
                               Color32 col, KuiContext ctx, float4 clip)
        {
            if (n <= 0) return;
            float thickness = math.max(1.5f, KuiDPI.Px(1.5f));

            if (n == 1)
            {
                float h = (data[0] / maxValue) * rect.w;
                ctx.CommandBuffer.PushRect(
                    new float4(rect.x + rect.z - math.max(2f, thickness),
                               rect.y + rect.w - h,
                               math.max(2f, thickness),
                               thickness),
                    col, clip);
                return;
            }

            for (int i = 0; i < n - 1; i++)
            {
                float h0 = (data[i]     / maxValue) * rect.w;
                float h1 = (data[i + 1] / maxValue) * rect.w;
                float x0 = xOrigin + i * colW;
                float x1 = xOrigin + (i + 1) * colW;
                float y0 = rect.y + rect.w - h0;
                float y1 = rect.y + rect.w - h1;

                ctx.CommandBuffer.PushLine(
                    new float2(x0, y0), new float2(x1, y1),
                    thickness, col, clip);
            }
        }

        static readonly Color32 KuiAvgColor        = new(80, 160, 220, 230); // cool blue (avg FPS)
        static readonly Color32 KuiBottleneckColor = new(220, 110, 80,  220); // hot red-orange (min FPS / bottleneck)
        static readonly Color32 KuiCapLineColor    = new(220, 220, 220, 200);
    }
}
