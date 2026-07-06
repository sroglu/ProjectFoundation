using System.Collections.Generic;

namespace PFound.Utilities.Pooling
{
    /// <summary>
    /// Static rent/return pool for <see cref="Dictionary{TKey,TValue}"/> — a thin, allocation-free
    /// facade over <see cref="UnityEngine.Pool.DictionaryPool{TKey,TValue}"/> with a ref-nulling
    /// return so callers holding a field can hand the reference back and have it cleared in one call.
    /// </summary>
    public static class DictionaryPool<TKey, TValue>
    {
        /// <summary>Rent a cleared dictionary from the pool.</summary>
        public static Dictionary<TKey, TValue> Get() => UnityEngine.Pool.DictionaryPool<TKey, TValue>.Get();

        /// <summary>Return a dictionary to the pool and null the caller's reference. Safe on null.</summary>
        public static void Return(ref Dictionary<TKey, TValue> dictionary)
        {
            if (dictionary == null) return;
            UnityEngine.Pool.DictionaryPool<TKey, TValue>.Release(dictionary);
            dictionary = null;
        }

        /// <summary>Alias of <see cref="Return"/>.</summary>
        public static void Release(ref Dictionary<TKey, TValue> dictionary) => Return(ref dictionary);
    }

    /// <summary>
    /// Static rent/return pool for <see cref="HashSet{T}"/> over
    /// <see cref="UnityEngine.Pool.HashSetPool{T}"/>, with a ref-nulling return.
    /// </summary>
    public static class HashSetPool<T>
    {
        /// <summary>Rent a cleared hash set from the pool.</summary>
        public static HashSet<T> Get() => UnityEngine.Pool.HashSetPool<T>.Get();

        /// <summary>Return a hash set to the pool and null the caller's reference. Safe on null.</summary>
        public static void Return(ref HashSet<T> set)
        {
            if (set == null) return;
            UnityEngine.Pool.HashSetPool<T>.Release(set);
            set = null;
        }

        /// <summary>Alias of <see cref="Return"/>.</summary>
        public static void Release(ref HashSet<T> set) => Return(ref set);
    }
}
