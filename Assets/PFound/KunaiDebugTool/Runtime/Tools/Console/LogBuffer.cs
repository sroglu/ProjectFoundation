using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Kunai
{
    /// <summary>
    /// Thread-safe log ingest + bounded ring buffer.
    /// Producers: Application.logMessageReceivedThreaded (any thread) → Enqueue.
    /// Consumer: main-thread Drain() inside KuiConsoleWindow.OnRenderUI before reading.
    /// </summary>
    internal sealed class KuiLogBuffer
    {
        readonly KuiLogEntry[] _ring;
        readonly int _capacity;
        readonly ConcurrentQueue<KuiLogEntry> _pending = new();
        int _head;
        int _count;

        // Per-level totals (cumulative — never decremented unless Clear).
        public int InfoCount;
        public int WarningCount;
        public int ErrorCount;

        // Reused frame-to-frame so CollapseConsecutive doesn't allocate.
        readonly List<RunSpan> _runs = new();

        public KuiLogBuffer(int capacity)
        {
            _capacity = capacity > 0 ? capacity : 1;
            _ring = new KuiLogEntry[_capacity];
        }

        public int Count => _count;
        public int Capacity => _capacity;

        public void Enqueue(in KuiLogEntry entry) => _pending.Enqueue(entry);

        /// <summary>
        /// Move pending entries from the producer queue into the ring. Main-thread
        /// only. Each newly captured entry's <see cref="KuiLogEntry.Category"/>
        /// is registered with <paramref name="categories"/> if non-null.
        /// </summary>
        public void Drain(KuiCategoryRegistry categories = null)
        {
            while (_pending.TryDequeue(out var e))
            {
                int idx = (_head + _count) % _capacity;
                if (_count == _capacity)
                {
                    // Overwriting oldest. Decrement its level counter so totals reflect what is in the ring.
                    DecrementLevel(_ring[idx].Level);
                    _head = (_head + 1) % _capacity;
                }
                else
                {
                    _count++;
                }
                _ring[idx] = e;
                IncrementLevel(e.Level);

                if (categories != null && !string.IsNullOrEmpty(e.Category))
                    categories.TryAdd(e.Category, out _);
            }
        }

        public void Clear()
        {
            _head = 0;
            _count = 0;
            InfoCount = WarningCount = ErrorCount = 0;
            // Drop pending too — clear means "fresh start".
            while (_pending.TryDequeue(out _)) { }
        }

        /// <summary>Index 0 = oldest visible entry, Count-1 = newest.</summary>
        public ref KuiLogEntry GetAt(int index) => ref _ring[(_head + index) % _capacity];

        /// <summary>
        /// Pair (firstIndex, runCount) representing a run of consecutive
        /// entries with identical (Level, Category, Message). Returned via
        /// <see cref="CollapseConsecutive"/>.
        /// </summary>
        public readonly struct RunSpan
        {
            public readonly int FirstIndex;
            public readonly int RunCount;
            public RunSpan(int first, int count) { FirstIndex = first; RunCount = count; }
        }

        /// <summary>
        /// Walk <paramref name="visibleIndices"/> in order and collapse runs of
        /// identical (Level, Category, Message) into <see cref="RunSpan"/>s.
        /// The returned list is reused frame-to-frame — copy if you need to
        /// keep it across <see cref="Drain"/> calls.
        /// </summary>
        public IReadOnlyList<RunSpan> CollapseConsecutive(IReadOnlyList<int> visibleIndices)
        {
            _runs.Clear();
            if (visibleIndices == null || visibleIndices.Count == 0) return _runs;

            int runStart = visibleIndices[0];
            int runLen   = 1;
            ref var prev = ref GetAt(runStart);

            for (int i = 1; i < visibleIndices.Count; i++)
            {
                int idx = visibleIndices[i];
                ref var cur = ref GetAt(idx);

                bool same = cur.Level == prev.Level
                         && string.Equals(cur.Category, prev.Category, System.StringComparison.Ordinal)
                         && string.Equals(cur.Message,  prev.Message,  System.StringComparison.Ordinal);

                if (same)
                {
                    runLen++;
                }
                else
                {
                    _runs.Add(new RunSpan(runStart, runLen));
                    runStart = idx;
                    runLen   = 1;
                    prev     = ref cur;
                }
            }
            _runs.Add(new RunSpan(runStart, runLen));
            return _runs;
        }

        void IncrementLevel(KuiLogLevel level)
        {
            switch (level)
            {
                case KuiLogLevel.Info:    InfoCount++; break;
                case KuiLogLevel.Warning: WarningCount++; break;
                case KuiLogLevel.Error:
                case KuiLogLevel.Exception: ErrorCount++; break;
            }
        }

        void DecrementLevel(KuiLogLevel level)
        {
            switch (level)
            {
                case KuiLogLevel.Info:    InfoCount--; break;
                case KuiLogLevel.Warning: WarningCount--; break;
                case KuiLogLevel.Error:
                case KuiLogLevel.Exception: ErrorCount--; break;
            }
        }
    }
}
