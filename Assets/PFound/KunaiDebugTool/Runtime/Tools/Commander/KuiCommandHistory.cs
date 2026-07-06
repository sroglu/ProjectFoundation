namespace Kunai
{
    /// <summary>
    /// Fixed-size circular history of entered command lines. The <see cref="Push"/>
    /// behaviour deduplicates back-to-back repeats (typing the same command
    /// twice doesn't create two history entries). Up/Down arrows in the
    /// Commander prompt walk via <see cref="Previous"/> / <see cref="Next"/>;
    /// committing the field calls <see cref="Reset"/> to drop the cursor back
    /// to "after the newest entry".
    /// </summary>
    internal sealed class KuiCommandHistory
    {
        public const int DefaultCapacity = 50;

        readonly string[] _ring;
        readonly int      _capacity;
        int _head;     // next write index
        int _count;    // number of valid entries (≤ capacity)
        int _cursor;   // -1 = "past newest"; 0..count-1 walks back through history

        public int Count => _count;
        public int Capacity => _capacity;

        public KuiCommandHistory(int capacity = DefaultCapacity)
        {
            _capacity = capacity > 0 ? capacity : 1;
            _ring = new string[_capacity];
            _cursor = -1;
        }

        public void Push(string line)
        {
            if (string.IsNullOrEmpty(line)) return;

            // Skip back-to-back duplicates — typing the same command twice
            // shouldn't pad the history.
            int newest = (_head - 1 + _capacity) % _capacity;
            if (_count > 0 && string.Equals(_ring[newest], line, System.StringComparison.Ordinal))
            {
                _cursor = -1;
                return;
            }

            _ring[_head] = line;
            _head = (_head + 1) % _capacity;
            if (_count < _capacity) _count++;
            _cursor = -1;
        }

        /// <summary>Walk back one entry. Returns null when no older entry exists.</summary>
        public string Previous()
        {
            if (_count == 0) return null;
            int next = _cursor + 1;
            if (next >= _count) return PeekAtCursor();   // already at oldest
            _cursor = next;
            return PeekAtCursor();
        }

        /// <summary>Walk forward one entry; returns null past the newest.</summary>
        public string Next()
        {
            if (_count == 0 || _cursor < 0) return null;
            int next = _cursor - 1;
            if (next < 0) { _cursor = -1; return string.Empty; }   // past newest → empty line
            _cursor = next;
            return PeekAtCursor();
        }

        public void Reset() => _cursor = -1;

        public void Clear()
        {
            for (int i = 0; i < _capacity; i++) _ring[i] = null;
            _head   = 0;
            _count  = 0;
            _cursor = -1;
        }

        string PeekAtCursor()
        {
            if (_cursor < 0 || _cursor >= _count) return null;
            int idx = (_head - 1 - _cursor + _capacity) % _capacity;
            return _ring[idx];
        }
    }
}
