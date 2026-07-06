using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PFound.Utilities.SerializableCollections
{
    /// <summary>
    /// A <see cref="Dictionary{TKey,TValue}"/> that Unity can serialize (and show in the inspector).
    /// The live dictionary is the source of truth at runtime; parallel key/value lists mirror it
    /// only across serialization boundaries via <see cref="ISerializationCallbackReceiver"/>.
    /// Duplicate or null keys produced by inspector edits are dropped on deserialize.
    /// </summary>
    [Serializable]
    public class SerializedDictionary<TKey, TValue> : IDictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        [SerializeField] private List<TKey> _keys = new List<TKey>();
        [SerializeField] private List<TValue> _values = new List<TValue>();

        private readonly Dictionary<TKey, TValue> _map = new Dictionary<TKey, TValue>();

        public SerializedDictionary() { }

        public SerializedDictionary(IDictionary<TKey, TValue> source)
        {
            if (source != null)
            {
                foreach (var pair in source)
                    _map[pair.Key] = pair.Value;
            }
        }

        // ---- IDictionary ----------------------------------------------------

        public TValue this[TKey key]
        {
            get => _map[key];
            set => _map[key] = value;
        }

        public ICollection<TKey> Keys => _map.Keys;
        public ICollection<TValue> Values => _map.Values;
        public int Count => _map.Count;
        public bool IsReadOnly => false;

        public void Add(TKey key, TValue value) => _map.Add(key, value);
        public bool ContainsKey(TKey key) => _map.ContainsKey(key);
        public bool Remove(TKey key) => _map.Remove(key);
        public bool TryGetValue(TKey key, out TValue value) => _map.TryGetValue(key, out value);
        public void Clear() => _map.Clear();

        public void Add(KeyValuePair<TKey, TValue> item) => _map.Add(item.Key, item.Value);

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return _map.TryGetValue(item.Key, out var existing)
                   && EqualityComparer<TValue>.Default.Equals(existing, item.Value);
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return Contains(item) && _map.Remove(item.Key);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            ((ICollection<KeyValuePair<TKey, TValue>>)_map).CopyTo(array, arrayIndex);
        }

        public Dictionary<TKey, TValue>.Enumerator GetEnumerator() => _map.GetEnumerator();
        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => _map.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _map.GetEnumerator();

        // ---- Serialization bridge ------------------------------------------

        public void OnBeforeSerialize()
        {
            _keys.Clear();
            _values.Clear();
            _keys.Capacity = _map.Count;
            _values.Capacity = _map.Count;
            foreach (var pair in _map)
            {
                _keys.Add(pair.Key);
                _values.Add(pair.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            _map.Clear();
            int count = Math.Min(_keys.Count, _values.Count);
            for (int i = 0; i < count; i++)
            {
                var key = _keys[i];
                if (key == null)
                    continue;
                _map[key] = _values[i]; // last write wins on duplicate keys
            }
        }
    }
}
