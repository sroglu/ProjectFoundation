using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace PFound.Utilities.Pooling
{
    /// <summary>
    /// Thin ergonomic sugar over <see cref="UnityEngine.Pool"/>. The stdlib collection pools already
    /// exist and are used verbatim underneath; what they lack is a scoped disposable that also exposes
    /// the borrowed instance directly. These readonly structs provide a
    /// <c>using var scope = PooledList&lt;T&gt;.New();</c> pattern where <c>scope.Value</c> (or an
    /// implicit conversion) hands back the collection, and disposal returns it to the pool.
    /// </summary>
    public readonly struct PooledList<T> : IDisposable
    {
        public readonly List<T> Value;

        private PooledList(List<T> value) => Value = value;

        /// <summary>Rents a cleared list from <see cref="ListPool{T}"/>.</summary>
        public static PooledList<T> New() => new PooledList<T>(ListPool<T>.Get());

        /// <summary>Returns the list to the pool. Safe to call once per <see cref="New"/>.</summary>
        public void Dispose()
        {
            if (Value != null) ListPool<T>.Release(Value);
        }

        public static implicit operator List<T>(PooledList<T> pooled) => pooled.Value;
    }

    /// <summary>Scoped disposable facade over <see cref="HashSetPool{T}"/>.</summary>
    public readonly struct PooledHashSet<T> : IDisposable
    {
        public readonly HashSet<T> Value;

        private PooledHashSet(HashSet<T> value) => Value = value;

        public static PooledHashSet<T> New() => new PooledHashSet<T>(UnityEngine.Pool.HashSetPool<T>.Get());

        public void Dispose()
        {
            if (Value != null) UnityEngine.Pool.HashSetPool<T>.Release(Value);
        }

        public static implicit operator HashSet<T>(PooledHashSet<T> pooled) => pooled.Value;
    }

    /// <summary>Scoped disposable facade over <see cref="DictionaryPool{TKey,TValue}"/>.</summary>
    public readonly struct PooledDictionary<TKey, TValue> : IDisposable
    {
        public readonly Dictionary<TKey, TValue> Value;

        private PooledDictionary(Dictionary<TKey, TValue> value) => Value = value;

        public static PooledDictionary<TKey, TValue> New() =>
            new PooledDictionary<TKey, TValue>(UnityEngine.Pool.DictionaryPool<TKey, TValue>.Get());

        public void Dispose()
        {
            if (Value != null) UnityEngine.Pool.DictionaryPool<TKey, TValue>.Release(Value);
        }

        public static implicit operator Dictionary<TKey, TValue>(PooledDictionary<TKey, TValue> pooled) => pooled.Value;
    }
}
