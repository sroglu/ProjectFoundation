using System;
using System.Threading;
using System.Threading.Tasks;
using PFound.StartupOrchestration;

namespace PFound.SampleGame.Steps
{
    /// <summary>
    /// A stand-in boot step: it just waits, ticking <see cref="IProgress{T}"/> a few times so the
    /// aggregate bar visibly advances, then completes. Real steps would download a catalog, fetch
    /// translations, or warm up systems here — each still dependency-free and fail-soft per the
    /// <see cref="IStartupStep"/> contract.
    /// </summary>
    public sealed class DelayStartupStep : IStartupStep
    {
        private const int Ticks = 5;

        private readonly WaitReason _reason;
        private readonly float _weight;
        private readonly int _tickDelayMs;

        public DelayStartupStep(WaitReason reason, float weight, int totalDelayMs)
        {
            _reason = reason;
            _weight = weight;
            _tickDelayMs = totalDelayMs / Ticks;
        }

        public WaitReason Reason => _reason;

        public float Weight => _weight;

        public async Task ExecuteAsync(IProgress<float> progress, CancellationToken ct)
        {
            for (int i = 1; i <= Ticks; i++)
            {
                await Task.Delay(_tickDelayMs, ct);
                progress.Report(i / (float)Ticks);
            }
        }
    }
}
