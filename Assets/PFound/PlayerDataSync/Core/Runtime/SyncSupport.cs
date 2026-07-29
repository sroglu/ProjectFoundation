using System;
using System.Threading;
using System.Threading.Tasks;

namespace PFound.PlayerDataSync.Core
{
    /// <summary>Wall-clock source. Injected so the "last seen" stamp and any time-based logic are deterministic in tests.</summary>
    public interface ISyncClock
    {
        long NowUnixMs { get; }
    }

    /// <summary>System clock backed by UTC now. Engine-free (no UnityEngine), usable from both the Unity layer and tests.</summary>
    public sealed class SystemSyncClock : ISyncClock
    {
        public long NowUnixMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>Awaitable delay for retry backoff. Injected so tests can resolve instantly instead of waiting real seconds.</summary>
    public interface ISyncDelay
    {
        Task Wait(TimeSpan duration, CancellationToken cancellationToken);
    }

    /// <summary>Real delay backed by <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.</summary>
    public sealed class TaskSyncDelay : ISyncDelay
    {
        public Task Wait(TimeSpan duration, CancellationToken cancellationToken) => Task.Delay(duration, cancellationToken);
    }

    /// <summary>
    /// Retry schedule for a backend push: how many attempts, and the exponential backoff between them
    /// (capped). A transient network failure is retried rather than lost; once attempts are exhausted the
    /// write stays queued locally for the next flush, so it is deferred, never dropped.
    /// </summary>
    public readonly struct RetryPolicy
    {
        public readonly int MaxAttempts;
        public readonly TimeSpan BaseDelay;
        public readonly double BackoffMultiplier;
        public readonly TimeSpan MaxDelay;

        public RetryPolicy(int maxAttempts, TimeSpan baseDelay, double backoffMultiplier, TimeSpan maxDelay)
        {
            MaxAttempts = maxAttempts;
            BaseDelay = baseDelay;
            BackoffMultiplier = backoffMultiplier;
            MaxDelay = maxDelay;
        }

        public static RetryPolicy Default => new RetryPolicy(4, TimeSpan.FromSeconds(1), 2.0, TimeSpan.FromSeconds(30));

        /// <summary>The wait before the given zero-based retry attempt, growing geometrically and capped at <see cref="MaxDelay"/>.</summary>
        public TimeSpan DelayForAttempt(int attemptIndex)
        {
            double ms = BaseDelay.TotalMilliseconds * Math.Pow(BackoffMultiplier, attemptIndex);
            double capped = Math.Min(ms, MaxDelay.TotalMilliseconds);
            return TimeSpan.FromMilliseconds(capped);
        }
    }

    /// <summary>What a flush did.</summary>
    public enum SyncStatus
    {
        /// <summary>There was no pending local write; nothing to push.</summary>
        NothingPending,

        /// <summary>The pending local write was pushed to the backend and cleared.</summary>
        Synced,

        /// <summary>The backend had a newer save (another device); it was adopted locally and the local pending write was superseded.</summary>
        AdoptedRemote,

        /// <summary>The backend was unreachable or the push failed after all retries; the write stays queued locally for the next flush.</summary>
        Deferred
    }

    /// <summary>The result of a flush: the status plus the save the engine now holds as current.</summary>
    public readonly struct SyncOutcome
    {
        public readonly SyncStatus Status;
        public readonly PlayerSave Current;

        public SyncOutcome(SyncStatus status, PlayerSave current)
        {
            Status = status;
            Current = current;
        }
    }
}
