#if BACKEND
using System;
using System.Collections.Generic;

namespace PFound.NetworkLayer
{
    /// <summary>
    /// Richer, opcode-resolved server telemetry sitting alongside the coarse
    /// <see cref="ServerMetrics"/> counters. It answers three operational questions:
    ///   - per message type: how many served and how long they took (min / max / mean);
    ///   - throughput: how many messages cleared in the most recent time window;
    ///   - deadline health: fire an event the instant a request outlives its serve
    ///     deadline, so a host can page/log without polling.
    ///
    /// Timing is kept as running min/max/sum-plus-count per opcode — exact mean with
    /// O(1) update and no per-sample allocation, and no reliance on any particular
    /// summary statistic. The throughput window is closed on the pump tick, keeping
    /// all bookkeeping on the server's own thread.
    /// </summary>
    public sealed class ServerDiagnostics
    {
        /// <summary>Per-opcode service record. Mean is derived, so it can never drift
        /// out of step with the samples.</summary>
        public struct OpcodeStat
        {
            public long Served;
            public long MinMs;
            public long MaxMs;
            public long SumMs;
            public double MeanMs => Served == 0 ? 0.0 : (double)SumMs / Served;
        }

        public struct ThroughputSample
        {
            public long WindowMs;
            public long Served;
            public double PerSecond;
        }

        public struct DeadlineMiss
        {
            public ushort Opcode;
            public int Peer;
            public long ElapsedMs;
        }

        readonly Dictionary<ushort, OpcodeStat> _stats = new Dictionary<ushort, OpcodeStat>();
        readonly int _windowMs;

        long _windowOpenedMs;
        long _windowServed;
        bool _windowStarted;

        /// <summary>Raised once per completed throughput window.</summary>
        public event Action<ThroughputSample> ThroughputReported;

        /// <summary>Raised the moment the watchdog reclaims a request past its deadline.</summary>
        public event Action<DeadlineMiss> DeadlineExceeded;

        public IReadOnlyDictionary<ushort, OpcodeStat> PerOpcode => _stats;
        public long DeadlineMissCount { get; private set; }

        public ServerDiagnostics(int windowMs)
        {
            _windowMs = windowMs;
        }

        public bool TryGet(ushort opcode, out OpcodeStat stat) => _stats.TryGetValue(opcode, out stat);

        internal void RecordServed(ushort opcode, long serviceMs, long nowMs)
        {
            OpcodeStat s;
            if (_stats.TryGetValue(opcode, out s))
            {
                s.Served++;
                s.SumMs += serviceMs;
                if (serviceMs < s.MinMs) s.MinMs = serviceMs;
                if (serviceMs > s.MaxMs) s.MaxMs = serviceMs;
            }
            else
            {
                s = new OpcodeStat { Served = 1, MinMs = serviceMs, MaxMs = serviceMs, SumMs = serviceMs };
            }
            _stats[opcode] = s;

            if (!_windowStarted)
            {
                _windowStarted = true;
                _windowOpenedMs = nowMs;
            }
            _windowServed++;
        }

        internal void RecordDeadlineMiss(ushort opcode, int peer, long elapsedMs)
        {
            DeadlineMissCount++;
            var handler = DeadlineExceeded;
            if (handler != null)
                handler(new DeadlineMiss { Opcode = opcode, Peer = peer, ElapsedMs = elapsedMs });
        }

        /// <summary>Close the throughput window and emit a sample if it has elapsed.</summary>
        internal void Tick(long nowMs)
        {
            if (!_windowStarted)
                return;
            long span = nowMs - _windowOpenedMs;
            if (span < _windowMs)
                return;

            var handler = ThroughputReported;
            if (handler != null)
            {
                double perSecond = span > 0 ? _windowServed * 1000.0 / span : 0.0;
                handler(new ThroughputSample { WindowMs = span, Served = _windowServed, PerSecond = perSecond });
            }
            _windowStarted = false;
            _windowServed = 0;
        }

        public void Reset()
        {
            _stats.Clear();
            _windowStarted = false;
            _windowServed = 0;
            DeadlineMissCount = 0;
        }
    }
}
#endif
