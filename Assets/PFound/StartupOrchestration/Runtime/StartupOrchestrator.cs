using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace PFound.StartupOrchestration
{
    /// <summary>
    /// Runs every registered <see cref="IStartupStep"/> concurrently and reports an aggregated
    /// <see cref="StartupAggregate"/> per tick. Returns when all steps complete (success or
    /// fail-soft — each step owns its fallback). A step that throws is caught, logged, and counted
    /// as completed so boot cannot hang. No concurrency cap and no deadline (v1).
    ///
    /// This type is independent of any DI container.
    /// </summary>
    public sealed class StartupOrchestrator
    {
        private readonly List<IStartupStep> _steps = new();

        /// <summary>Adds a step. Idempotent on instance identity.</summary>
        public void Register(IStartupStep step)
        {
            if (step == null) throw new ArgumentNullException(nameof(step));
            if (!_steps.Contains(step)) _steps.Add(step);
        }

        /// <summary>
        /// Runs all registered steps in parallel, reporting a monotonic aggregate to
        /// <paramref name="progress"/>. Completes when every step finishes (success or fail-soft).
        /// </summary>
        public async Task RunAsync(IProgress<StartupAggregate> progress, CancellationToken ct)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));

            int count = _steps.Count;
            if (count == 0)
            {
                progress.Report(new StartupAggregate(
                    1f, default, Array.Empty<(string, WaitReason, float)>()));
                return;
            }

            var run = new RunState(_steps, progress);
            var tasks = new Task[count];
            for (int i = 0; i < count; i++)
                tasks[i] = run.RunStepAsync(i, ct);

            await Task.WhenAll(tasks);
            run.EmitFinal();
        }

        /// <summary>
        /// Per-run mutable state, so a single orchestrator instance can be run more than once and
        /// concurrent step progress reports stay consistent.
        /// </summary>
        private sealed class RunState
        {
            private readonly IReadOnlyList<IStartupStep> _steps;
            private readonly IProgress<StartupAggregate>  _sink;
            private readonly float[] _progress;
            private readonly float[] _weights;     // per-step bar weight (captured once at run start)
            private readonly float   _weightTotal;
            private readonly long[]  _seq;        // report order, for dominant-reason tie-break
            private readonly object  _gate = new();
            private long  _seqCounter;
            private float _emittedMax;

            public RunState(IReadOnlyList<IStartupStep> steps, IProgress<StartupAggregate> sink)
            {
                _steps    = steps;
                _sink     = sink;
                _progress = new float[steps.Count];
                _seq      = new long[steps.Count];
                _weights  = new float[steps.Count];
                float total = 0f;
                for (int i = 0; i < steps.Count; i++)
                {
                    var w = steps[i].Weight;
                    if (w < 0f) w = 0f;             // a stray negative weight must not subtract progress
                    _weights[i] = w;
                    total += w;
                }
                // All-zero weights would divide by zero; fall back to equal weight so the bar still moves.
                if (total <= 0f)
                {
                    for (int i = 0; i < steps.Count; i++) _weights[i] = 1f;
                    total = steps.Count;
                }
                _weightTotal = total;
            }

            public async Task RunStepAsync(int index, CancellationToken ct)
            {
                var stepProgress = new StepProgress(this, index);
                try
                {
                    await _steps[index].ExecuteAsync(stepProgress, ct);
                    Report(index, 1f);
                }
                catch (OperationCanceledException)
                {
                    // App quit — propagate so boot stops; not a step failure to swallow.
                    throw;
                }
                catch (Exception ex)
                {
                    // Boundary catch: arbitrary plugged-in step code. Log loudly, count the step as
                    // done so RunAsync can't hang. The step's owner is expected to have reported 1f +
                    // fallen back already; this is the safety net.
                    Debug.LogError(
                        $"[StartupOrchestrator] Step '{_steps[index].GetType().Name}' " +
                        $"({_steps[index].Reason}) threw and was treated as completed: {ex}");
                    Report(index, 1f);
                }
            }

            public void Report(int index, float value)
            {
                lock (_gate)
                {
                    _progress[index] = Mathf.Clamp01(value);
                    _seq[index]      = ++_seqCounter;
                    Emit();
                }
            }

            public void EmitFinal()
            {
                lock (_gate)
                {
                    for (int i = 0; i < _progress.Length; i++) _progress[i] = 1f;
                    Emit();
                }
            }

            // Caller holds _gate.
            private void Emit()
            {
                float weightedSum = 0f;
                for (int i = 0; i < _progress.Length; i++) weightedSum += _weights[i] * _progress[i];
                float overall = Mathf.Max(weightedSum / _weightTotal, _emittedMax); // weighted + monotonic
                _emittedMax = overall;

                int dom = 0;
                for (int i = 1; i < _progress.Length; i++)
                {
                    bool lower   = _progress[i] < _progress[dom];
                    bool tieNewer = _progress[i] == _progress[dom] && _seq[i] > _seq[dom];
                    if (lower || tieNewer) dom = i;
                }

                var per = new (string, WaitReason, float)[_progress.Length];
                for (int i = 0; i < _progress.Length; i++)
                    per[i] = (_steps[i].GetType().Name, _steps[i].Reason, _progress[i]);

                _sink.Report(new StartupAggregate(overall, _steps[dom].Reason, per));
            }
        }

        private sealed class StepProgress : IProgress<float>
        {
            private readonly RunState _run;
            private readonly int      _index;

            public StepProgress(RunState run, int index)
            {
                _run   = run;
                _index = index;
            }

            public void Report(float value) => _run.Report(_index, value);
        }
    }
}
