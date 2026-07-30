using System;
using System.Collections.Generic;
using System.Reflection;

namespace PFound.ECS
{
    /// <summary>
    /// Single source of truth for "which types are registrable systems", shared by the editor
    /// code generator (compile-time list) and the runtime reflection fallback. The generated
    /// registry is the fast path; <see cref="FromLoadedAssemblies"/> is the parity oracle a test
    /// compares against, and a usable fallback when no generated registry is present.
    /// </summary>
    public static class SystemDiscovery
    {
        /// <summary>
        /// A type qualifies if it is a concrete <see cref="SystemBase"/>, externally visible, and
        /// has a public parameterless constructor — i.e. the generated registry can write
        /// <c>new T()</c> for it from another assembly. Stricter than
        /// <see cref="World.DiscoverSystems"/> (which can reflect non-public ctors); that strictness
        /// is intentional so the generated code compiles everywhere.
        /// </summary>
        public static bool IsSystemType(Type t)
        {
            if (t == null) return false;
            if (!t.IsClass || t.IsAbstract) return false;
            if (t.IsGenericTypeDefinition) return false;
            if (!t.IsVisible) return false; // public and all enclosing types public
            if (!typeof(SystemBase).IsAssignableFrom(t)) return false;
            return t.GetConstructor(Type.EmptyTypes) != null;
        }

        /// <summary>Filters <paramref name="types"/> to registrable systems, ordered deterministically by full name.</summary>
        public static List<Type> SelectSystems(IEnumerable<Type> types)
        {
            if (types == null) throw new ArgumentNullException(nameof(types));
            var systems = new List<Type>();
            foreach (var t in types)
                if (IsSystemType(t)) systems.Add(t);
            systems.Sort(CompareByFullName);
            return systems;
        }

        /// <summary>
        /// Reflection over every loaded assembly. The parity oracle and the runtime fallback;
        /// the editor generator exists to make this scan unnecessary at runtime.
        /// </summary>
        public static List<Type> FromLoadedAssemblies()
        {
            var systems = new List<Type>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types; } // partial load: keep what resolved
                foreach (var t in types)
                    if (IsSystemType(t)) systems.Add(t);
            }
            systems.Sort(CompareByFullName);
            return systems;
        }

        private static int CompareByFullName(Type a, Type b) =>
            string.CompareOrdinal(a.FullName, b.FullName);
    }
}
