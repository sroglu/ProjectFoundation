using System;
using System.Collections.Generic;
using System.Reflection;

namespace PFound.Utilities.Reflection
{
    /// <summary>
    /// Memoizes two reflection lookups that are expensive to repeat but stable for the lifetime of an
    /// AppDomain: "all types carrying attribute T" and "the assembly whose name equals X". Results are
    /// cached on first request; call <see cref="Invalidate"/> after dynamically loading assemblies.
    /// Instance-based so callers can scope or replace a cache; a shared <see cref="Default"/> is provided.
    /// </summary>
    public sealed class ReflectionCache
    {
        /// <summary>Process-wide shared cache for the common case.</summary>
        public static readonly ReflectionCache Default = new ReflectionCache();

        private readonly object _gate = new object();
        private readonly Dictionary<Type, Type[]> _typesByAttribute = new Dictionary<Type, Type[]>();
        private Dictionary<string, Assembly> _assembliesByName;

        /// <summary>Returns (and caches) every type annotated with <typeparamref name="TAttribute"/>
        /// found across the current AppDomain.</summary>
        public IReadOnlyList<Type> TypesWithAttribute<TAttribute>(bool inherit = true)
            where TAttribute : Attribute
        {
            var key = typeof(TAttribute);
            lock (_gate)
            {
                if (_typesByAttribute.TryGetValue(key, out var cached)) return cached;

                var collected = new List<Type>();
                foreach (var type in MemberReflection.FindTypesWithAttribute<TAttribute>(inherit))
                {
                    collected.Add(type);
                }
                var result = collected.ToArray();
                _typesByAttribute[key] = result;
                return result;
            }
        }

        /// <summary>Returns the loaded assembly whose simple name matches <paramref name="name"/>
        /// exactly (ordinal), or <c>null</c> when no such assembly is loaded.</summary>
        public Assembly FindAssemblyByExactName(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            lock (_gate)
            {
                if (_assembliesByName == null) BuildAssemblyIndex();
                return _assembliesByName.TryGetValue(name, out var assembly) ? assembly : null;
            }
        }

        /// <summary>Clears every cached result. Call after loading assemblies at runtime.</summary>
        public void Invalidate()
        {
            lock (_gate)
            {
                _typesByAttribute.Clear();
                _assembliesByName = null;
            }
        }

        private void BuildAssemblyIndex()
        {
            var index = new Dictionary<string, Assembly>(StringComparer.Ordinal);
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var simpleName = assembly.GetName().Name;
                // First writer wins if duplicate simple names ever appear (side-by-side loads).
                if (!index.ContainsKey(simpleName)) index.Add(simpleName, assembly);
            }
            _assembliesByName = index;
        }
    }
}
