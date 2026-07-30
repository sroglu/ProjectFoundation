using System;
using System.Collections.Generic;

namespace PFound.NetworkLayer
{
    /// <summary>
    /// Client-side round-trip latency, measured per request type (per opcode). For
    /// every call it keeps how many completed, the fastest and slowest round-trip,
    /// and the running total — so the average is exact with an O(1), no-allocation
    /// update and never drifts out of step with the samples.
    ///
    /// "Round-trip" here is wall time from the moment the client hands the request
    /// to the link until the matching reply settles (or the call faults). It is the
    /// time the caller actually waited, so it includes the network hop and the
    /// server's own service time — a strictly wider view than the server's
    /// <see cref="ServerDiagnostics"/> service-time record.
    ///
    /// It also raises <see cref="SlowCall"/> the instant a call's round-trip crosses
    /// a caller-set threshold, so a host can log or alert without polling the table.
    /// This lives on the client, is not server-only, and holds no engine types, so it
    /// is fully testable outside Unity.
    /// </summary>
    public sealed class CallLatencyStats
    {
        /// <summary>Per-opcode round-trip record. The mean is derived from the sum and
        /// count, so it can never disagree with the samples it summarizes.</summary>
        public struct OpcodeLatency
        {
            public long Completed;
            public long MinMs;
            public long MaxMs;
            public long SumMs;
            public double AverageMs => Completed == 0 ? 0.0 : (double)SumMs / Completed;
        }

        /// <summary>Details handed to the <see cref="SlowCall"/> listener.</summary>
        public struct SlowCallReport
        {
            public ushort Opcode;
            public long RoundTripMs;
            public long ThresholdMs;
        }

        readonly Dictionary<ushort, OpcodeLatency> _byOpcode = new Dictionary<ushort, OpcodeLatency>();

        /// <summary>Round-trips at or above this many milliseconds raise
        /// <see cref="SlowCall"/>. Zero or below turns the event off.</summary>
        public long SlowCallThresholdMs { get; set; }

        /// <summary>How many completed calls have crossed the slow threshold so far.</summary>
        public long SlowCallCount { get; private set; }

        /// <summary>Raised the moment a completed call's round-trip reaches the
        /// threshold. The legacy request-time-exceeded callback, expressed per opcode
        /// with the actual round-trip rather than only the threshold.</summary>
        public event Action<SlowCallReport> SlowCall;

        public CallLatencyStats(long slowCallThresholdMs = 0)
        {
            SlowCallThresholdMs = slowCallThresholdMs;
        }

        /// <summary>Live, read-only view of the per-opcode table.</summary>
        public IReadOnlyDictionary<ushort, OpcodeLatency> PerOpcode => _byOpcode;

        public bool TryGet(ushort opcode, out OpcodeLatency latency) => _byOpcode.TryGetValue(opcode, out latency);

        /// <summary>Fold one completed round-trip into the opcode's record and, if it
        /// is at or over the threshold, raise <see cref="SlowCall"/>.</summary>
        internal void RecordRoundTrip(ushort opcode, long roundTripMs)
        {
            OpcodeLatency stat;
            if (_byOpcode.TryGetValue(opcode, out stat))
            {
                stat.Completed++;
                stat.SumMs += roundTripMs;
                if (roundTripMs < stat.MinMs) stat.MinMs = roundTripMs;
                if (roundTripMs > stat.MaxMs) stat.MaxMs = roundTripMs;
            }
            else
            {
                stat = new OpcodeLatency
                {
                    Completed = 1,
                    MinMs = roundTripMs,
                    MaxMs = roundTripMs,
                    SumMs = roundTripMs,
                };
            }
            _byOpcode[opcode] = stat;

            if (SlowCallThresholdMs > 0 && roundTripMs >= SlowCallThresholdMs)
            {
                SlowCallCount++;
                var handler = SlowCall;
                if (handler != null)
                    handler(new SlowCallReport
                    {
                        Opcode = opcode,
                        RoundTripMs = roundTripMs,
                        ThresholdMs = SlowCallThresholdMs,
                    });
            }
        }

        public void Reset()
        {
            _byOpcode.Clear();
            SlowCallCount = 0;
        }
    }
}
