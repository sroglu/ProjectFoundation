using System;
using System.Collections.Generic;

namespace PFound.Utilities.Collections
{
    /// <summary>
    /// General-purpose, engine-free extension helpers for collections.
    /// Only members that go meaningfully beyond a single LINQ call are included.
    /// </summary>
    public static class CollectionExtensions
    {
        // ---- Emptiness ------------------------------------------------------

        /// <summary>True when the collection reference is null or holds no elements.</summary>
        public static bool IsNullOrEmpty<T>(this ICollection<T> collection)
        {
            return collection == null || collection.Count == 0;
        }

        /// <summary>True when the collection holds at least one element (and is not null).</summary>
        public static bool HasAny<T>(this ICollection<T> collection)
        {
            return collection != null && collection.Count != 0;
        }

        // ---- Unique add -----------------------------------------------------

        /// <summary>
        /// Appends <paramref name="item"/> only if an equal element is not already present.
        /// Returns true when the item was added.
        /// </summary>
        public static bool AddUnique<T>(this IList<T> list, T item)
        {
            if (list.Contains(item))
                return false;
            list.Add(item);
            return true;
        }

        /// <summary>Appends every item from <paramref name="items"/> that is not already present. Returns how many were added.</summary>
        public static int AddRangeUnique<T>(this IList<T> list, IEnumerable<T> items)
        {
            int added = 0;
            foreach (var item in items)
            {
                if (list.AddUnique(item))
                    added++;
            }
            return added;
        }

        // ---- Sorted insertion ----------------------------------------------

        /// <summary>
        /// Inserts <paramref name="item"/> keeping the list in ascending order (assuming it already is).
        /// Uses a binary search so the operation is O(log n) comparisons + O(n) shift.
        /// Returns the index the item was inserted at.
        /// </summary>
        public static int InsertSorted<T>(this IList<T> list, T item, IComparer<T> comparer = null)
        {
            comparer = comparer ?? Comparer<T>.Default;
            int index = BinarySearchIndex(list, item, comparer);
            if (index < 0)
                index = ~index; // first element strictly greater
            list.Insert(index, item);
            return index;
        }

        /// <summary>
        /// Like <see cref="InsertSorted{T}"/> but skips the insert when an equal element already exists.
        /// Returns the insertion index, or -1 when the item was already present.
        /// </summary>
        public static int InsertSortedUnique<T>(this IList<T> list, T item, IComparer<T> comparer = null)
        {
            comparer = comparer ?? Comparer<T>.Default;
            int index = BinarySearchIndex(list, item, comparer);
            if (index >= 0)
                return -1; // exact match found
            index = ~index;
            list.Insert(index, item);
            return index;
        }

        // Returns a non-negative index of a matching element, otherwise the bitwise
        // complement of the first index whose element is greater than 'item'.
        private static int BinarySearchIndex<T>(IList<T> list, T item, IComparer<T> comparer)
        {
            int low = 0;
            int high = list.Count - 1;
            while (low <= high)
            {
                int mid = low + ((high - low) >> 1);
                int cmp = comparer.Compare(list[mid], item);
                if (cmp == 0)
                    return mid;
                if (cmp < 0)
                    low = mid + 1;
                else
                    high = mid - 1;
            }
            return ~low;
        }

        // ---- Null scrubbing -------------------------------------------------

        /// <summary>Removes every null reference from the list in place. Returns the count removed.</summary>
        public static int RemoveNulls<T>(this IList<T> list) where T : class
        {
            int removed = 0;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] == null)
                {
                    list.RemoveAt(i);
                    removed++;
                }
            }
            return removed;
        }

        // ---- Dictionary get-or-create --------------------------------------

        /// <summary>
        /// Returns the value stored for <paramref name="key"/>, constructing and storing a new
        /// default instance when the key is absent.
        /// </summary>
        public static TValue GetOrCreate<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
            where TValue : new()
        {
            if (!dictionary.TryGetValue(key, out var value))
            {
                value = new TValue();
                dictionary[key] = value;
            }
            return value;
        }

        /// <summary>
        /// Returns the value stored for <paramref name="key"/>, invoking <paramref name="factory"/>
        /// to build and store one when the key is absent.
        /// </summary>
        public static TValue GetOrCreate<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TValue> factory)
        {
            if (!dictionary.TryGetValue(key, out var value))
            {
                value = factory();
                dictionary[key] = value;
            }
            return value;
        }

        // ---- Dictionary of lists -------------------------------------------

        /// <summary>Adds <paramref name="value"/> to the list stored under <paramref name="key"/>, creating the list if needed.</summary>
        public static void AddToList<TKey, TValue>(this IDictionary<TKey, List<TValue>> dictionary, TKey key, TValue value)
        {
            if (!dictionary.TryGetValue(key, out var list))
            {
                list = new List<TValue>();
                dictionary[key] = list;
            }
            list.Add(value);
        }

        /// <summary>
        /// Removes <paramref name="value"/> from the list stored under <paramref name="key"/>.
        /// When the list becomes empty the key is dropped. Returns true when a value was removed.
        /// </summary>
        public static bool RemoveFromList<TKey, TValue>(this IDictionary<TKey, List<TValue>> dictionary, TKey key, TValue value)
        {
            if (!dictionary.TryGetValue(key, out var list))
                return false;
            bool removed = list.Remove(value);
            if (list.Count == 0)
                dictionary.Remove(key);
            return removed;
        }

        // ---- Counter dictionary --------------------------------------------

        /// <summary>Adds <paramref name="amount"/> (default 1) to the counter stored under <paramref name="key"/> and returns the new total.</summary>
        public static int Increment<TKey>(this IDictionary<TKey, int> counters, TKey key, int amount = 1)
        {
            counters.TryGetValue(key, out var current);
            int updated = current + amount;
            counters[key] = updated;
            return updated;
        }

        /// <summary>Subtracts <paramref name="amount"/> (default 1) from the counter and returns the new total.</summary>
        public static int Decrement<TKey>(this IDictionary<TKey, int> counters, TKey key, int amount = 1)
        {
            return counters.Increment(key, -amount);
        }

        // ---- Duplicates -----------------------------------------------------

        /// <summary>Returns one entry per value that appears more than once in the source.</summary>
        public static List<T> FindDuplicates<T>(this IEnumerable<T> source, IEqualityComparer<T> comparer = null)
        {
            var seen = new HashSet<T>(comparer ?? EqualityComparer<T>.Default);
            var reported = new HashSet<T>(comparer ?? EqualityComparer<T>.Default);
            var result = new List<T>();
            foreach (var item in source)
            {
                if (!seen.Add(item) && reported.Add(item))
                    result.Add(item);
            }
            return result;
        }

        /// <summary>
        /// Removes later occurrences of already-seen values from the list in place,
        /// preserving the order of first appearance. Returns the count removed.
        /// </summary>
        public static int RemoveDuplicates<T>(this IList<T> list, IEqualityComparer<T> comparer = null)
        {
            var seen = new HashSet<T>(comparer ?? EqualityComparer<T>.Default);
            int removed = 0;
            for (int i = 0; i < list.Count; )
            {
                if (seen.Add(list[i]))
                {
                    i++;
                }
                else
                {
                    list.RemoveAt(i);
                    removed++;
                }
            }
            return removed;
        }

        // ---- Reordering -----------------------------------------------------

        /// <summary>Moves the element at <paramref name="oldIndex"/> so it ends up at <paramref name="newIndex"/>.</summary>
        public static void Move<T>(this IList<T> list, int oldIndex, int newIndex)
        {
            if (oldIndex == newIndex)
                return;
            T item = list[oldIndex];
            list.RemoveAt(oldIndex);
            list.Insert(newIndex, item);
        }

        /// <summary>Swaps the elements at two indices.</summary>
        public static void Swap<T>(this IList<T> list, int a, int b)
        {
            if (a == b)
                return;
            (list[a], list[b]) = (list[b], list[a]);
        }

        // ---- Fill -----------------------------------------------------------

        /// <summary>Sets every slot of the list to <paramref name="value"/>.</summary>
        public static void Fill<T>(this IList<T> list, T value)
        {
            for (int i = 0; i < list.Count; i++)
                list[i] = value;
        }

        /// <summary>Sets every slot of the list using <paramref name="factory"/>, which receives the index.</summary>
        public static void Fill<T>(this IList<T> list, Func<int, T> factory)
        {
            for (int i = 0; i < list.Count; i++)
                list[i] = factory(i);
        }

        // ---- Front dequeue --------------------------------------------------

        /// <summary>Removes and returns the first element of the list (FIFO-style front pop).</summary>
        public static T DequeueFront<T>(this IList<T> list)
        {
            if (list.Count == 0)
                throw new InvalidOperationException("The list is empty.");
            T item = list[0];
            list.RemoveAt(0);
            return item;
        }

        /// <summary>Removes and returns the first element, or the type default when the list is empty. Returns success via <paramref name="found"/>.</summary>
        public static T DequeueFrontOrDefault<T>(this IList<T> list, out bool found)
        {
            if (list.Count == 0)
            {
                found = false;
                return default;
            }
            found = true;
            return list.DequeueFront();
        }

        // ---- Capacity / resize ---------------------------------------------

        /// <summary>Grows the list's backing capacity to at least <paramref name="minCapacity"/> (never shrinks).</summary>
        public static void EnsureCapacity<T>(this List<T> list, int minCapacity)
        {
            if (list.Capacity < minCapacity)
                list.Capacity = minCapacity;
        }

        /// <summary>
        /// Reallocates <paramref name="array"/> to <paramref name="newSize"/>, keeping the overlapping
        /// prefix. Growing pads with default values; shrinking truncates.
        /// </summary>
        public static void Resize<T>(ref T[] array, int newSize)
        {
            if (newSize < 0)
                throw new ArgumentOutOfRangeException(nameof(newSize));
            if (array == null)
            {
                array = new T[newSize];
                return;
            }
            if (array.Length == newSize)
                return;
            var replacement = new T[newSize];
            int copy = array.Length < newSize ? array.Length : newSize;
            Array.Copy(array, replacement, copy);
            array = replacement;
        }

        // ---- Array concatenation -------------------------------------------

        /// <summary>Returns a new array containing the elements of <paramref name="first"/> followed by <paramref name="second"/>.</summary>
        public static T[] Concat<T>(this T[] first, T[] second)
        {
            first = first ?? Array.Empty<T>();
            second = second ?? Array.Empty<T>();
            var result = new T[first.Length + second.Length];
            Array.Copy(first, 0, result, 0, first.Length);
            Array.Copy(second, 0, result, first.Length, second.Length);
            return result;
        }

        /// <summary>Returns a new array that concatenates any number of source arrays in order.</summary>
        public static T[] ConcatAll<T>(params T[][] arrays)
        {
            int total = 0;
            for (int i = 0; i < arrays.Length; i++)
            {
                if (arrays[i] != null)
                    total += arrays[i].Length;
            }
            var result = new T[total];
            int offset = 0;
            for (int i = 0; i < arrays.Length; i++)
            {
                var part = arrays[i];
                if (part == null || part.Length == 0)
                    continue;
                Array.Copy(part, 0, result, offset, part.Length);
                offset += part.Length;
            }
            return result;
        }

        // ---- Byte-sequence search ------------------------------------------

        /// <summary>
        /// Returns the index of the first occurrence of the <paramref name="pattern"/> byte sequence
        /// within <paramref name="buffer"/> at or after <paramref name="startIndex"/>, or -1 if not found.
        /// An empty pattern matches at <paramref name="startIndex"/>.
        /// </summary>
        public static int IndexOfSequence(this byte[] buffer, byte[] pattern, int startIndex = 0)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (pattern == null) throw new ArgumentNullException(nameof(pattern));
            if (startIndex < 0) startIndex = 0;
            if (pattern.Length == 0)
                return startIndex <= buffer.Length ? startIndex : -1;
            int last = buffer.Length - pattern.Length;
            for (int i = startIndex; i <= last; i++)
            {
                int j = 0;
                while (j < pattern.Length && buffer[i + j] == pattern[j])
                    j++;
                if (j == pattern.Length)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Returns the start index of every non-overlapping occurrence of <paramref name="pattern"/>
        /// within <paramref name="buffer"/>.
        /// </summary>
        public static List<int> IndexesOfSequence(this byte[] buffer, byte[] pattern)
        {
            var result = new List<int>();
            if (pattern == null || pattern.Length == 0)
                return result;
            int from = 0;
            while (true)
            {
                int hit = buffer.IndexOfSequence(pattern, from);
                if (hit < 0)
                    break;
                result.Add(hit);
                from = hit + pattern.Length; // non-overlapping
            }
            return result;
        }
    }
}
