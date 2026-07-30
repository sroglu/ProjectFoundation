using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace PFound.Utilities.Idle
{
    /// <summary>
    /// Watches for a lull in activity and reports an idle/active state. When no activity is
    /// registered for a configurable threshold the watcher becomes idle; the next activity
    /// signal immediately returns it to active. <see cref="OnIdleChanged"/> fires only on the
    /// active-&gt;idle and idle-&gt;active edges.
    /// </summary>
    /// <remarks>
    /// The activity source is injected rather than wired to a specific input backend: callers
    /// either push activity through <see cref="Poke"/> (e.g. from their own input handler) or
    /// supply an <c>activityProbe</c> delegate that the polling loop consults each tick. The
    /// loop is driven by UniTask (<see cref="UniTask.Delay(TimeSpan, DelayType, PlayerLoopTiming, CancellationToken, bool)"/>)
    /// and a <see cref="CancellationToken"/>, not a coroutine, so it needs no MonoBehaviour host.
    /// The clock is injectable to keep the transition logic deterministic under test.
    /// </remarks>
    public sealed class IdleWatcher : IDisposable
    {
        private readonly double _thresholdSeconds;
        private readonly TimeSpan _pollInterval;
        private readonly Func<bool> _activityProbe;
        private readonly Func<double> _clock;

        private double _lastActivityAt;
        private CancellationTokenSource _loopCts;
        private bool _disposed;

        /// <summary>Raised on idle/active edges only. Argument is the new idle state.</summary>
        public event Action<bool> OnIdleChanged;

        /// <summary>True while the watcher currently considers the user idle.</summary>
        public bool CurrentlyIdle { get; private set; }

        /// <summary>True between <see cref="Start"/> and <see cref="Stop"/>/<see cref="Dispose"/>.</summary>
        public bool IsRunning => _loopCts != null;

        /// <param name="idleThreshold">Quiet span after which the watcher flips to idle.</param>
        /// <param name="activityProbe">
        /// Optional per-tick check returning true when activity happened since the previous tick.
        /// Combine with or use instead of <see cref="Poke"/>.
        /// </param>
        /// <param name="pollInterval">
        /// How often the loop re-evaluates. Defaults to a small fraction of the threshold so the
        /// idle edge lands close to the configured moment.
        /// </param>
        /// <param name="clock">
        /// Monotonic seconds source. Defaults to Unity's unscaled realtime clock; injectable so
        /// tests can advance time by hand.
        /// </param>
        public IdleWatcher(
            TimeSpan idleThreshold,
            Func<bool> activityProbe = null,
            TimeSpan? pollInterval = null,
            Func<double> clock = null)
        {
            if (idleThreshold <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(idleThreshold), "Idle threshold must be positive.");
            }

            _thresholdSeconds = idleThreshold.TotalSeconds;
            _activityProbe = activityProbe;
            _clock = clock ?? DefaultClock;
            _pollInterval = pollInterval ?? DefaultPollInterval(idleThreshold);
            _lastActivityAt = _clock();
        }

        /// <summary>
        /// Registers activity right now. Resets the quiet timer and, if the watcher was idle,
        /// immediately transitions back to active (firing the edge).
        /// </summary>
        public void Poke()
        {
            _lastActivityAt = _clock();
            if (CurrentlyIdle)
            {
                SetIdle(false);
            }
        }

        /// <summary>Begins the polling loop. Calling again while running is a no-op.</summary>
        public void Start()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(IdleWatcher));
            }
            if (_loopCts != null)
            {
                return;
            }
            _lastActivityAt = _clock();
            _loopCts = new CancellationTokenSource();
            RunLoop(_loopCts.Token).Forget();
        }

        /// <summary>Stops the polling loop. State (CurrentlyIdle) is left as-is.</summary>
        public void Stop()
        {
            var cts = _loopCts;
            if (cts == null)
            {
                return;
            }
            _loopCts = null;
            cts.Cancel();
            cts.Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            Stop();
            OnIdleChanged = null;
        }

        /// <summary>
        /// Evaluates the idle condition once and fires any resulting edge. Called automatically
        /// by the internal loop, but also public so callers can drive the watcher from their own
        /// update loop (with an injected clock) instead of starting the UniTask loop.
        /// </summary>
        public void Tick()
        {
            if (_activityProbe != null && _activityProbe())
            {
                Poke();
                return;
            }

            bool quietLongEnough = (_clock() - _lastActivityAt) >= _thresholdSeconds;
            if (quietLongEnough != CurrentlyIdle)
            {
                SetIdle(quietLongEnough);
            }
        }

        private async UniTaskVoid RunLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await UniTask.Delay(_pollInterval, DelayType.UnscaledDeltaTime, PlayerLoopTiming.Update, token);
                    Tick();
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on Stop/Dispose.
            }
        }

        private void SetIdle(bool value)
        {
            CurrentlyIdle = value;
            OnIdleChanged?.Invoke(value);
        }

        private static double DefaultClock() => UnityEngine.Time.realtimeSinceStartupAsDouble;

        private static TimeSpan DefaultPollInterval(TimeSpan threshold)
        {
            // Poll roughly ten times across the threshold, clamped to a sane 50ms..1s window.
            double seconds = threshold.TotalSeconds / 10.0;
            if (seconds < 0.05) seconds = 0.05;
            if (seconds > 1.0) seconds = 1.0;
            return TimeSpan.FromSeconds(seconds);
        }
    }
}
