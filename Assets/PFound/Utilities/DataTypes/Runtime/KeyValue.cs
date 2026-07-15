using System;
using System.Collections.Generic;

namespace PFound.Utilities.DataType
{
    /// <summary>
    /// A mutable key/value pair. Unlike <see cref="System.Collections.Generic.KeyValuePair{TKey,TValue}"/>
    /// both fields are writable, which is convenient for serialized inspector lists and in-place edits.
    /// </summary>
    [Serializable]
    public struct KeyValue<TKey, TValue> : IEquatable<KeyValue<TKey, TValue>>
    {
        public TKey Key;
        public TValue Value;

        public KeyValue(TKey key, TValue value)
        {
            Key = key;
            Value = value;
        }

        public KeyValuePair<TKey, TValue> ToKeyValuePair() => new KeyValuePair<TKey, TValue>(Key, Value);

        public static implicit operator KeyValuePair<TKey, TValue>(KeyValue<TKey, TValue> pair) =>
            new KeyValuePair<TKey, TValue>(pair.Key, pair.Value);

        public static implicit operator KeyValue<TKey, TValue>(KeyValuePair<TKey, TValue> pair) =>
            new KeyValue<TKey, TValue>(pair.Key, pair.Value);

        public bool Equals(KeyValue<TKey, TValue> other) =>
            EqualityComparer<TKey>.Default.Equals(Key, other.Key) &&
            EqualityComparer<TValue>.Default.Equals(Value, other.Value);

        public override bool Equals(object obj) => obj is KeyValue<TKey, TValue> other && Equals(other);

        public override int GetHashCode()
        {
            var keyHash = Key == null ? 0 : Key.GetHashCode();
            var valueHash = Value == null ? 0 : Value.GetHashCode();
            return (keyHash * 397) ^ valueHash;
        }

        public override string ToString() => "[" + Key + ", " + Value + "]";
    }
}
