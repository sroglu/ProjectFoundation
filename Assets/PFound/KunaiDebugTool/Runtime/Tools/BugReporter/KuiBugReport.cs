using System;

namespace Kunai
{
    /// <summary>
    /// Snapshot of "what was the game doing when the user hit Capture".
    /// Built once per Capture and passed to every registered
    /// <see cref="IBugReportSink"/>. Properties are init-only so sinks can't
    /// mutate the payload they're handed.
    /// </summary>
    public sealed class KuiBugReport
    {
        public DateTime Timestamp        { get; init; }
        public string   Description      { get; init; } = "";
        public byte[]   ScreenshotPng    { get; init; } = Array.Empty<byte>();
        public string   ConsoleText      { get; init; } = "";
        public string   SystemInfoText   { get; init; } = "";
        public string   OutputDirectory  { get; init; } = "";
    }

    /// <summary>
    /// Extension seam for routing bug reports to external destinations
    /// (HTTP, Discord, Slack, S3, etc.). Phase 2 ships the seam only — no
    /// built-in implementations.
    /// </summary>
    /// <remarks>
    /// <para>Implementations MUST NOT throw; the bug-reporter pipeline
    /// catches and logs any exception. Implementations MAY be slow — the
    /// call already runs off the render thread.</para>
    /// </remarks>
    public interface IBugReportSink
    {
        void Send(KuiBugReport report);
    }
}
