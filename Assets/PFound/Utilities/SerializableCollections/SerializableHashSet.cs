using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PFound.Utilities.SerializableCollections
{
    /// <summary>
    /// A <see cref="HashSet{T}"/> that Unity can serialize (and show in the inspector).
    /// A backing item list mirrors the set only across serialization; the live set is authoritative
    /// at runtime. Duplicate/null entries introduced by inspector edits are collapsed on deserialize.
    /// </summary>
    [Serializable]
    public class SerializableHashSet<T> : ICollection<T>, ISerializationCallbackReceiver
    {
        [SerializeField] private List<T> _items = new List<T>();

        private readonly HashSet<T> _set = new HashSet<T>();

        public SerializableHashSet() { }

        public SerializableHashSet(IEnumerable<T> source)
        {
            if (source != null)
            {
                foreach (var item in source)
                    _set.Add(item);
            }
        }

        // ---- ICollection / set surface -------------------------------------

        public int Count => _set.Count;
        public bool IsReadOnly => false;

        /// <summary>Adds an item; returns true when it was not already present.</summary>
        public bool Add(T item) => _set.Add(item);

        void ICollection<T>.Add(T item) => _set.Add(item);

        public bool Contains(T item) => _set.Contains(item);
        public bool Remove(T item) => _set.Remove(item);
        public void Clear() => _set.Clear();
        public void CopyTo(T[] array, int arrayIndex) => _set.CopyTo(array, arrayIndex);

        public void UnionWith(IEnumerable<T> other) => _set.UnionWith(other);
        public void IntersectWith(IEnumerable<T> other) => _set.IntersectWith(other);
        public void ExceptWith(IEnumerable<T> other) => _set.ExceptWith(other);
        public bool IsSubsetOf(IEnumerable<T> other) => _set.IsSubsetOf(other);
        public bool IsSupersetOf(IEnumerable<T> other) => _set.IsSupersetOf(other);
        public bool Overlaps(IEnumerable<T> other) => _set.Overlaps(other);

        public HashSet<T>.Enumerator GetEnumerator() => _set.GetEnumerator();
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => _set.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _set.GetEnumerator();

        // ---- Serialization bridge ------------------------------------------

        public void OnBeforeSerialize()
        {
            _items.Clear();
            _items.Capacity = _set.Count;
            foreach (var item in _set)
                _items.Add(item);
        }

        public void OnAfterDeserialize()
        {
            _set.Clear();
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item == null)
                    continue;
                _set.Add(item); // duplicates ignored by the set
            }
        }
    }
}
