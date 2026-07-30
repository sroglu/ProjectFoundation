using System.Collections.Generic;

namespace PFound.InputRouter
{
    /// <summary>
    /// A minimal insertion-ordered map: one cohesive structure giving O(1) keyed lookup AND
    /// stable registration-order iteration. This is the canonical "linked hash map" shape — a
    /// hash index for lookup plus an order roster of keys for iteration — deliberately chosen so
    /// the router needs exactly ONE structure to satisfy both its typed-lookup and its
    /// deterministic-dispatch requirements. The order roster stores keys only (never a parallel
    /// copy of values); every value read still goes through the hash index.
    ///
    /// Registrations are rare relative to ticks, so <see cref="Remove"/> paying O(n) to keep the
    /// roster compact and ordered is a fine trade for an allocation-free <see cref="ValueAt"/>
    /// used on the per-tick path.
    /// </summary>
    internal sealed class InsertionOrderedMap<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> _byKey = new Dictionary<TKey, TValue>();
        private readonly List<TKey> _order = new List<TKey>();

        public int Count
        {
            get { return _order.Count; }
        }

        public bool Contains(TKey key)
        {
            return _byKey.ContainsKey(key);
        }

        public bool TryGet(TKey key, out TValue value)
        {
            return _byKey.TryGetValue(key, out value);
        }

        /// <summary>Append a new entry at the end of the order. Assumes the key is absent.</summary>
        public void Append(TKey key, TValue value)
        {
            _byKey.Add(key, value);
            _order.Add(key);
        }

        /// <summary>
        /// Remove an entry, closing the gap so the surviving entries keep their relative order.
        /// </summary>
        public void Remove(TKey key)
        {
            if (_byKey.Remove(key))
            {
                _order.Remove(key);
            }
        }

        /// <summary>The value at ordered position <paramref name="index"/> (registration order).</summary>
        public TValue ValueAt(int index)
        {
            return _byKey[_order[index]];
        }
    }
}
