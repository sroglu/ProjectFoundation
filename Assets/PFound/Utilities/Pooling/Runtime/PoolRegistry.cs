using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace PFound.Utilities.Pooling
{
    /// <summary>
    /// A tracked wrapper around <see cref="UnityEngine.Pool.ObjectPool{T}"/>. UnityEngine's own pools
    /// keep their pooled objects alive indefinitely with no built-in way to drain every pool at once
    /// (for example on scene teardown or a memory-pressure event). Pools created through
    /// <see cref="PoolRegistry.Create{T}"/> register themselves here so <see cref="PoolRegistry.DrainAll"/>
    /// can clear all of them in one call. This registry only sees pools it created — the static
    /// collection pools (<see cref="ListPool{T}"/> etc.) are intentionally left untouched.
    /// </summary>
    public static class PoolRegistry
    {
        private static readonly List<IDrainablePool> Registered = new List<IDrainablePool>();

        /// <summary>Number of pools currently registered.</summary>
        public static int RegisteredCount
        {
            get { lock (Registered) return Registered.Count; }
        }

        /// <summary>
        /// Creates an <see cref="UnityEngine.Pool.ObjectPool{T}"/> and registers it for global draining.
        /// Parameters mirror the Unity constructor so it stays a thin pass-through.
        /// </summary>
        public static ObjectPool<T> Create<T>(
            Func<T> createFunc,
            Action<T> onGet = null,
            Action<T> onRelease = null,
            Action<T> onDestroy = null,
            bool collectionCheck = true,
            int defaultCapacity = 10,
            int maxSize = 10000) where T : class
        {
            if (createFunc == null) throw new ArgumentNullException(nameof(createFunc));

            var pool = new ObjectPool<T>(createFunc, onGet, onRelease, onDestroy,
                collectionCheck, defaultCapacity, maxSize);

            lock (Registered)
            {
                Registered.Add(new DrainablePool<T>(pool));
            }
            return pool;
        }

        /// <summary>Clears every registered pool, releasing their retained instances.</summary>
        public static void DrainAll()
        {
            lock (Registered)
            {
                for (var i = 0; i < Registered.Count; i++)
                {
                    Registered[i].Clear();
                }
            }
        }

        /// <summary>Drains and forgets every registered pool.</summary>
        public static void DrainAndClearRegistry()
        {
            lock (Registered)
            {
                for (var i = 0; i < Registered.Count; i++)
                {
                    Registered[i].Clear();
                }
                Registered.Clear();
            }
        }

        private interface IDrainablePool
        {
            void Clear();
        }

        private sealed class DrainablePool<T> : IDrainablePool where T : class
        {
            private readonly ObjectPool<T> _pool;
            public DrainablePool(ObjectPool<T> pool) => _pool = pool;
            public void Clear() => _pool.Clear();
        }
    }
}
