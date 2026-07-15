using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Kunai
{
    internal sealed class KuiOptionEntry
    {
        public MemberInfo  Member;
        public Type        MemberType;
        public string      Label;
        public string      Category;
        public double?     Min;
        public double?     Max;
        public Func<object>      Get;
        public Action<object>    Set;
    }

    /// <summary>
    /// Inspector-side registry of every <c>[KuiOption]</c>-tagged static
    /// member found in the AppDomain. Built lazily on first window open via
    /// <see cref="KuiReflectionScanner"/>; entries are grouped by
    /// <c>[KuiCategory]</c> (or "Default" if absent).
    /// </summary>
    internal sealed class KuiOptionRegistry
    {
        public const string DefaultCategory = "Default";

        // Insertion-order list so the first observed category renders first.
        readonly List<string> _categoryOrder = new();
        readonly Dictionary<string, List<KuiOptionEntry>> _byCategory = new(StringComparer.Ordinal);

        public IReadOnlyList<string> Categories => _categoryOrder;
        public int Count
        {
            get
            {
                int n = 0;
                foreach (var kv in _byCategory) n += kv.Value.Count;
                return n;
            }
        }

        public IReadOnlyList<KuiOptionEntry> InCategory(string category)
        {
            return _byCategory.TryGetValue(category, out var list) ? list : System.Array.Empty<KuiOptionEntry>();
        }

        public KuiReflectionResult BuildFromScan()
        {
            _categoryOrder.Clear();
            _byCategory.Clear();

            return KuiReflectionScanner.Scan<KuiOptionAttribute>((member, attr) =>
            {
                Type memberType;
                Func<object>   getter;
                Action<object> setter;

                if (member is FieldInfo f)
                {
                    if (!f.IsStatic)
                    {
                        Warn($"'{member.Name}' on {member.DeclaringType?.FullName} is an instance field — skipped.");
                        return;
                    }
                    memberType = f.FieldType;
                    if (!IsSupportedType(memberType))
                    {
                        Warn($"'{member.Name}': unsupported type {memberType.FullName} — skipped.");
                        return;
                    }
                    getter = ()      => f.GetValue(null);
                    setter = v       => f.SetValue(null, v);
                }
                else if (member is PropertyInfo p)
                {
                    var getMi = p.GetGetMethod(true);
                    if (getMi == null || !getMi.IsStatic)
                    {
                        Warn($"'{member.Name}' on {member.DeclaringType?.FullName} is an instance property — skipped.");
                        return;
                    }
                    memberType = p.PropertyType;
                    if (!IsSupportedType(memberType))
                    {
                        Warn($"'{member.Name}': unsupported type {memberType.FullName} — skipped.");
                        return;
                    }
                    var setMi = p.GetSetMethod(true);
                    getter = () => p.GetValue(null, null);
                    setter = setMi != null
                        ? (Action<object>)(v => p.SetValue(null, v, null))
                        : (_ => { /* read-only property — ignore writes */ });
                }
                else
                {
                    return;
                }

                var rangeAttr = member.GetCustomAttribute<KuiRangeAttribute>();
                var catAttr   = member.GetCustomAttribute<KuiCategoryAttribute>();

                var entry = new KuiOptionEntry
                {
                    Member     = member,
                    MemberType = memberType,
                    Label      = attr.Label ?? member.Name,
                    Category   = catAttr?.Name ?? DefaultCategory,
                    Min        = rangeAttr?.Min,
                    Max        = rangeAttr?.Max,
                    Get        = getter,
                    Set        = setter,
                };

                if (!_byCategory.TryGetValue(entry.Category, out var bucket))
                {
                    bucket = new List<KuiOptionEntry>();
                    _byCategory[entry.Category] = bucket;
                    _categoryOrder.Add(entry.Category);
                }
                bucket.Add(entry);
            });
        }

        internal static bool IsSupportedType(Type t)
        {
            if (t == typeof(bool))   return true;
            if (t == typeof(int))    return true;
            if (t == typeof(float))  return true;
            if (t == typeof(double)) return true;
            if (t == typeof(string)) return true;
            if (t.IsEnum)            return true;
            return false;
        }

        static void Warn(string msg) => Debug.LogWarning("[KuiOption] " + msg);
    }
}
