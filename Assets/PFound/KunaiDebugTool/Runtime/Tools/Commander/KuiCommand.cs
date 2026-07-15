using System;

namespace Kunai
{
    /// <summary>
    /// Marks a static method as invokable from the Kunai Commander REPL.
    /// Supported parameter types: <c>string</c>, <c>int</c>, <c>float</c>,
    /// <c>double</c>, <c>bool</c>, and any <c>enum</c>. Methods with other
    /// parameter types are skipped with a warning at registration time.
    /// Async methods (<c>Task</c>/<c>UniTask</c>) are out of scope for v1.
    /// Return value is converted via <c>ToString()</c> and printed in the
    /// Commander output; <c>void</c> prints <c>"OK"</c>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class KuiCommandAttribute : Attribute
    {
        public string Name { get; }
        public string Help { get; }

        public KuiCommandAttribute(string name, string help = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Command name must be non-empty.", nameof(name));
            if (name.Length > 31)
                throw new ArgumentException("Command name must be ≤ 31 chars.", nameof(name));
            if (name.IndexOf('[') >= 0)
                throw new ArgumentException("Command name must not contain '['.", nameof(name));
            for (int i = 0; i < name.Length; i++)
                if (char.IsWhiteSpace(name[i]))
                    throw new ArgumentException("Command name must not contain whitespace.", nameof(name));
            Name = name;
            Help = help;
        }
    }

    /// <summary>
    /// Groups [KuiOption] / [KuiCommand] members into a named category. Used
    /// by both D4 (Inspector groups) and D5 (Commander help groups). Members
    /// without [KuiCategory] fall under the implicit <c>"Default"</c> group.
    /// First D4/D5 to ship owns this attribute; the other reuses it (D5 ships
    /// first in the planned sequence).
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method,
                    AllowMultiple = false)]
    public sealed class KuiCategoryAttribute : Attribute
    {
        public string Name { get; }

        public KuiCategoryAttribute(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Category name must be non-empty.", nameof(name));
            if (name.Length > 31)
                throw new ArgumentException("Category name must be ≤ 31 chars.", nameof(name));
            if (name.IndexOf('[') >= 0)
                throw new ArgumentException("Category name must not contain '['.", nameof(name));
            Name = name;
        }
    }
}
