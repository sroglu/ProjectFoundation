using System;
using System.Collections.Generic;
using UnityEngine;

namespace PFound.Utilities.MathTools.Unity
{
    /// <summary>
    /// Random selection helpers over lists: pick a random element or index, an
    /// in-place Fisher-Yates shuffle, and a predicate-filtered random pick.
    /// Uses UnityEngine.Random by default; overloads accept a <see cref="System.Random"/>
    /// for deterministic/seeded use.
    /// </summary>
    public static class RandomMath
    {
        // ---- Random index / element -------------------------------------------

        public static int RandomIndex<T>(IReadOnlyList<T> list)
        {
            return UnityEngine.Random.Range(0, list.Count);
        }

        public static int RandomIndex<T>(IReadOnlyList<T> list, System.Random random)
        {
            return random.Next(list.Count);
        }

        public static T RandomElement<T>(IReadOnlyList<T> list)
        {
            return list[RandomIndex(list)];
        }

        public static T RandomElement<T>(IReadOnlyList<T> list, System.Random random)
        {
            return list[RandomIndex(list, random)];
        }

        // ---- Fisher-Yates in-place shuffle -------------------------------------

        public static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                Swap(list, i, j);
            }
        }

        public static void Shuffle<T>(IList<T> list, System.Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                Swap(list, i, j);
            }
        }

        // ---- Filtered random pick ----------------------------------------------

        /// <summary>
        /// Randomly picks one element that satisfies <paramref name="predicate"/> using
        /// reservoir sampling, so the source is scanned once and no temporary list is built.
        /// Sets <paramref name="chosen"/> and returns false when nothing matches.
        /// </summary>
        public static bool TryRandomWhere<T>(IReadOnlyList<T> list, Func<T, bool> predicate, out T chosen)
        {
            chosen = default;
            int seen = 0;
            for (int i = 0; i < list.Count; i++)
            {
                T candidate = list[i];
                if (!predicate(candidate))
                {
                    continue;
                }

                seen++;
                if (UnityEngine.Random.Range(0, seen) == 0)
                {
                    chosen = candidate;
                }
            }
            return seen > 0;
        }

        private static void Swap<T>(IList<T> list, int a, int b)
        {
            T tmp = list[a];
            list[a] = list[b];
            list[b] = tmp;
        }
    }
}
