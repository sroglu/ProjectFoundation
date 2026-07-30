using System.Collections.Generic;

namespace Kunai
{
    /// <summary>
    /// Tracks every distinct category observed during a session, in arrival
    /// order, capped at <see cref="MaxCategories"/>. Beyond the cap, names go
    /// to <see cref="Overflowed"/> and the chip strip gets a single "…" overflow
    /// chip (UI concern). Bounded UI prevents accidental dynamic categories
    /// (e.g., per-entity tags) from blowing up the toolbar.
    /// </summary>
    internal sealed class KuiCategoryRegistry
    {
        public const int MaxCategories = 32;

        // Insertion-ordered list of distinct in-cap categories. Index doubles
        // as the chip-strip slot.
        readonly List<string> _list = new(MaxCategories);

        // Hash for O(1) "have we seen this?". Mirrors _list contents.
        readonly HashSet<string> _seen = new();

        // Names beyond the cap. Lookup answers "is this overflowed?" so the
        // UI can route filter clicks on the overflow chip to all of them.
        readonly HashSet<string> _overflowed = new();

        public IReadOnlyList<string> Categories => _list;
        public IReadOnlyCollection<string> Overflowed => _overflowed;
        public int Count => _list.Count;
        public int OverflowCount => _overflowed.Count;

        /// <summary>
        /// Add a category if it's new. Returns true on first sight (in-cap or
        /// overflowed). <paramref name="index"/> = slot in <see cref="Categories"/>
        /// for in-cap names; -1 if overflowed or already known. Null/empty
        /// inputs are ignored.
        /// </summary>
        public bool TryAdd(string category, out int index)
        {
            index = -1;
            if (string.IsNullOrEmpty(category)) return false;
            if (_seen.Contains(category)) return false;
            if (_overflowed.Contains(category)) return false;

            if (_list.Count < MaxCategories)
            {
                _list.Add(category);
                _seen.Add(category);
                index = _list.Count - 1;
                return true;
            }

            _overflowed.Add(category);
            return true;
        }

        public bool IsOverflowed(string category)
        {
            return !string.IsNullOrEmpty(category) && _overflowed.Contains(category);
        }

        public void Clear()
        {
            _list.Clear();
            _seen.Clear();
            _overflowed.Clear();
        }
    }
}
