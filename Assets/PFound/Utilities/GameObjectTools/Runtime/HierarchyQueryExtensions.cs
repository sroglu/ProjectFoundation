using System;
using System.Collections.Generic;
using UnityEngine;

namespace PFound.Utilities.GameObjectTools
{
    /// <summary>
    /// Read-only hierarchy queries: ancestry tests, recursive name searches
    /// (exact / prefix / suffix / contains) and direct-child enumeration.
    /// Searches walk children depth-first without allocating on the exact/first-match paths.
    /// </summary>
    public static class HierarchyQueryExtensions
    {
        /// <summary>True when <paramref name="candidate"/> lies anywhere below
        /// <paramref name="ancestor"/> in the hierarchy. A transform is not a descendant of itself.
        /// Named distinctly from Unity's built-in <c>Transform.IsChildOf</c> (which is an instance
        /// method that would shadow an equally-named extension, and treats self as a child).</summary>
        public static bool IsDescendantOf(this Transform candidate, Transform ancestor)
        {
            if (candidate == null || ancestor == null || candidate == ancestor)
                return false;

            Transform walker = candidate.parent;
            while (walker != null)
            {
                if (walker == ancestor)
                    return true;
                walker = walker.parent;
            }
            return false;
        }

        /// <summary>True when <paramref name="candidate"/> is an ancestor of
        /// <paramref name="descendant"/>. Mirror of <see cref="IsDescendantOf"/>.</summary>
        public static bool IsParentOf(this Transform candidate, Transform descendant)
        {
            return descendant.IsDescendantOf(candidate);
        }

        /// <summary>
        /// Depth-first search for the first descendant whose name equals
        /// <paramref name="name"/> exactly. Returns null when absent.
        /// </summary>
        public static Transform FindChildByName(this Transform root, string name)
        {
            return FindDescendant(root, name, NameMatch.Exact);
        }

        /// <summary>First descendant whose name starts with <paramref name="prefix"/>.</summary>
        public static Transform FindChildStartingWith(this Transform root, string prefix)
        {
            return FindDescendant(root, prefix, NameMatch.Prefix);
        }

        /// <summary>First descendant whose name ends with <paramref name="suffix"/>.</summary>
        public static Transform FindChildEndingWith(this Transform root, string suffix)
        {
            return FindDescendant(root, suffix, NameMatch.Suffix);
        }

        /// <summary>First descendant whose name contains <paramref name="fragment"/>.</summary>
        public static Transform FindChildContaining(this Transform root, string fragment)
        {
            return FindDescendant(root, fragment, NameMatch.Contains);
        }

        /// <summary>All descendants whose name starts with <paramref name="prefix"/>.</summary>
        public static List<Transform> FindChildrenStartingWith(this Transform root, string prefix, List<Transform> results = null)
        {
            return CollectDescendants(root, prefix, NameMatch.Prefix, results);
        }

        /// <summary>All descendants whose name ends with <paramref name="suffix"/>.</summary>
        public static List<Transform> FindChildrenEndingWith(this Transform root, string suffix, List<Transform> results = null)
        {
            return CollectDescendants(root, suffix, NameMatch.Suffix, results);
        }

        /// <summary>All descendants whose name contains <paramref name="fragment"/>.</summary>
        public static List<Transform> FindChildrenContaining(this Transform root, string fragment, List<Transform> results = null)
        {
            return CollectDescendants(root, fragment, NameMatch.Contains, results);
        }

        /// <summary>
        /// Collects the immediate children of <paramref name="parent"/> into a list.
        /// Reuses <paramref name="results"/> when provided to avoid allocation.
        /// </summary>
        public static List<Transform> GetDirectChildren(this Transform parent, List<Transform> results = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (results == null) results = new List<Transform>(parent.childCount);
            else results.Clear();

            int count = parent.childCount;
            for (int i = 0; i < count; i++)
                results.Add(parent.GetChild(i));
            return results;
        }

        /// <summary>
        /// Invokes <paramref name="action"/> for each immediate child. The snapshot of
        /// indices is taken up front so re-parenting inside the callback is safe.
        /// </summary>
        public static void ForEachDirectChild(this Transform parent, Action<Transform> action)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (action == null) throw new ArgumentNullException(nameof(action));

            // Snapshot children first so the callback may safely detach/destroy them.
            int count = parent.childCount;
            if (count == 0) return;

            var snapshot = new Transform[count];
            for (int i = 0; i < count; i++)
                snapshot[i] = parent.GetChild(i);
            for (int i = 0; i < count; i++)
            {
                Transform child = snapshot[i];
                if (child != null)
                    action(child);
            }
        }

        private enum NameMatch { Exact, Prefix, Suffix, Contains }

        private static bool Matches(string candidate, string token, NameMatch mode)
        {
            switch (mode)
            {
                case NameMatch.Exact: return candidate == token;
                case NameMatch.Prefix: return candidate.StartsWith(token, StringComparison.Ordinal);
                case NameMatch.Suffix: return candidate.EndsWith(token, StringComparison.Ordinal);
                case NameMatch.Contains: return candidate.IndexOf(token, StringComparison.Ordinal) >= 0;
                default: return false;
            }
        }

        private static Transform FindDescendant(Transform root, string token, NameMatch mode)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            int count = root.childCount;
            for (int i = 0; i < count; i++)
            {
                Transform child = root.GetChild(i);
                if (Matches(child.name, token, mode))
                    return child;

                Transform deeper = FindDescendant(child, token, mode);
                if (deeper != null)
                    return deeper;
            }
            return null;
        }

        private static List<Transform> CollectDescendants(Transform root, string token, NameMatch mode, List<Transform> results)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (results == null) results = new List<Transform>();
            else results.Clear();
            AppendMatches(root, token, mode, results);
            return results;
        }

        private static void AppendMatches(Transform root, string token, NameMatch mode, List<Transform> results)
        {
            int count = root.childCount;
            for (int i = 0; i < count; i++)
            {
                Transform child = root.GetChild(i);
                if (Matches(child.name, token, mode))
                    results.Add(child);
                AppendMatches(child, token, mode, results);
            }
        }
    }
}
