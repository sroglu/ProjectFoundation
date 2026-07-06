using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Kunai
{
    internal sealed class KuiCommandEntry
    {
        public string         Name;
        public string         Help;
        public string         Category;     // from [KuiCategory] or "Default"
        public MethodInfo     Method;
        public ParameterInfo[] Parameters;  // primitive / enum only
    }

    /// <summary>
    /// Name → <see cref="KuiCommandEntry"/> dictionary built by walking every
    /// loaded assembly via <see cref="KuiReflectionScanner"/> and collecting
    /// methods tagged with <see cref="KuiCommandAttribute"/>. Instance methods
    /// and methods with unsupported parameter types are skipped with a warning.
    /// </summary>
    internal sealed class KuiCommandRegistry
    {
        public const string DefaultCategory = "Default";

        readonly Dictionary<string, KuiCommandEntry> _byName =
            new(StringComparer.Ordinal);

        // Sorted name list for prefix completion. Built in BuildFromScan;
        // searches walk it linearly (typically < 50 entries).
        readonly List<string> _sortedNames = new();

        public IReadOnlyDictionary<string, KuiCommandEntry> ByName => _byName;
        public IReadOnlyList<string> SortedNames => _sortedNames;
        public int Count => _byName.Count;

        public bool TryGet(string name, out KuiCommandEntry entry)
        {
            if (string.IsNullOrEmpty(name)) { entry = null; return false; }
            return _byName.TryGetValue(name, out entry);
        }

        /// <summary>Names whose lowercase form starts with <paramref name="prefix"/>.</summary>
        public IEnumerable<string> Match(string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
            {
                foreach (var n in _sortedNames) yield return n;
                yield break;
            }
            for (int i = 0; i < _sortedNames.Count; i++)
            {
                var n = _sortedNames[i];
                if (n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    yield return n;
            }
        }

        /// <summary>
        /// Run the reflection scan once and populate the registry. Safe to call
        /// repeatedly; clears prior entries first. Errors per assembly land in
        /// the returned <see cref="KuiReflectionResult"/> so the caller can
        /// surface them in the Commander output if desired.
        /// </summary>
        public KuiReflectionResult BuildFromScan()
        {
            _byName.Clear();
            _sortedNames.Clear();

            var result = KuiReflectionScanner.Scan<KuiCommandAttribute>((member, attr) =>
            {
                if (member is not MethodInfo mi) return;

                if (!mi.IsStatic)
                {
                    Debug.LogWarning(
                        $"[KuiCommand] '{attr.Name}' on {mi.DeclaringType?.FullName}.{mi.Name} " +
                        "is an instance method — skipped (only static methods are supported).");
                    return;
                }

                var prms = mi.GetParameters();
                bool ok = true;
                for (int i = 0; i < prms.Length; i++)
                {
                    if (!IsSupportedParam(prms[i].ParameterType))
                    {
                        Debug.LogWarning(
                            $"[KuiCommand] '{attr.Name}': parameter '{prms[i].Name}' has unsupported " +
                            $"type {prms[i].ParameterType.FullName} — skipped.");
                        ok = false;
                        break;
                    }
                }
                if (!ok) return;

                if (_byName.ContainsKey(attr.Name))
                {
                    Debug.LogWarning($"[KuiCommand] duplicate name '{attr.Name}' — keeping the first registration.");
                    return;
                }

                var catAttr = mi.GetCustomAttribute<KuiCategoryAttribute>();

                _byName[attr.Name] = new KuiCommandEntry
                {
                    Name       = attr.Name,
                    Help       = attr.Help,
                    Category   = catAttr?.Name ?? DefaultCategory,
                    Method     = mi,
                    Parameters = prms,
                };
            });

            // Stable sort gives predictable Tab-completion ordering.
            foreach (var n in _byName.Keys) _sortedNames.Add(n);
            _sortedNames.Sort(StringComparer.OrdinalIgnoreCase);

            return result;
        }

        // Mirrors the supported set in attributes.md.
        internal static bool IsSupportedParam(Type t)
        {
            if (t == typeof(string))  return true;
            if (t == typeof(int))     return true;
            if (t == typeof(float))   return true;
            if (t == typeof(double))  return true;
            if (t == typeof(bool))    return true;
            if (t.IsEnum)             return true;
            return false;
        }
    }
}
