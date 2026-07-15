using System;
using System.Collections.Generic;
using System.Reflection;

namespace PFound.Utilities.Reflection
{
    /// <summary>
    /// Small, allocation-conscious helpers around <see cref="System.Reflection"/> that cover the
    /// operations most commonly needed by tooling: reading a single custom attribute, pulling a
    /// field value by name, locating methods, and classifying property accessors. Engine-free so it
    /// compiles and runs under plain mono/csc.
    /// </summary>
    public static class MemberReflection
    {
        private const BindingFlags InstanceFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private const BindingFlags StaticFlags =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        // ---- Attributes -----------------------------------------------------

        /// <summary>Returns the first attribute of type <typeparamref name="TAttribute"/> declared on
        /// <paramref name="member"/>, or <c>null</c> when none is present.</summary>
        public static TAttribute GetAttribute<TAttribute>(this MemberInfo member, bool inherit = true)
            where TAttribute : Attribute
        {
            if (member == null) throw new ArgumentNullException(nameof(member));
            var found = member.GetCustomAttributes(typeof(TAttribute), inherit);
            return found.Length > 0 ? (TAttribute)found[0] : null;
        }

        /// <summary>Returns the first attribute of type <typeparamref name="TAttribute"/> declared on
        /// <paramref name="type"/>, or <c>null</c> when none is present.</summary>
        public static TAttribute GetAttribute<TAttribute>(this Type type, bool inherit = true)
            where TAttribute : Attribute
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            var found = type.GetCustomAttributes(typeof(TAttribute), inherit);
            return found.Length > 0 ? (TAttribute)found[0] : null;
        }

        /// <summary>True when <paramref name="member"/> carries at least one
        /// <typeparamref name="TAttribute"/>.</summary>
        public static bool HasAttribute<TAttribute>(this MemberInfo member, bool inherit = true)
            where TAttribute : Attribute
        {
            if (member == null) throw new ArgumentNullException(nameof(member));
            return member.IsDefined(typeof(TAttribute), inherit);
        }

        // ---- Field access ---------------------------------------------------

        /// <summary>Reads the value of the instance field <paramref name="fieldName"/> from
        /// <paramref name="instance"/> and casts it to <typeparamref name="TValue"/>.</summary>
        public static TValue GetInstanceFieldValue<TValue>(object instance, string fieldName)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            var field = ResolveField(instance.GetType(), fieldName, InstanceFlags);
            return (TValue)field.GetValue(instance);
        }

        /// <summary>Reads the value of the static field <paramref name="fieldName"/> declared on
        /// <paramref name="owner"/> and casts it to <typeparamref name="TValue"/>.</summary>
        public static TValue GetStaticFieldValue<TValue>(Type owner, string fieldName)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            var field = ResolveField(owner, fieldName, StaticFlags);
            return (TValue)field.GetValue(null);
        }

        private static FieldInfo ResolveField(Type type, string fieldName, BindingFlags flags)
        {
            if (string.IsNullOrEmpty(fieldName)) throw new ArgumentNullException(nameof(fieldName));
            // Walk the declaration chain so private fields on base classes are still reachable.
            for (var t = type; t != null; t = t.BaseType)
            {
                var field = t.GetField(fieldName, flags | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            throw new MissingFieldException(type.FullName, fieldName);
        }

        // ---- Method lookup --------------------------------------------------

        /// <summary>Finds an instance method by name (or <c>null</c> if absent).</summary>
        public static MethodInfo GetInstanceMethod(Type owner, string methodName)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (string.IsNullOrEmpty(methodName)) throw new ArgumentNullException(nameof(methodName));
            return owner.GetMethod(methodName, InstanceFlags);
        }

        /// <summary>Finds a static method by name (or <c>null</c> if absent).</summary>
        public static MethodInfo GetStaticMethod(Type owner, string methodName)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (string.IsNullOrEmpty(methodName)) throw new ArgumentNullException(nameof(methodName));
            return owner.GetMethod(methodName, StaticFlags);
        }

        // ---- Property accessor classification -------------------------------

        /// <summary>True when <paramref name="method"/> is the get-accessor of a property.</summary>
        public static bool IsPropertyGetter(this MethodInfo method)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));
            return method.IsSpecialName && method.Name.StartsWith("get_", StringComparison.Ordinal);
        }

        /// <summary>True when <paramref name="method"/> is the set-accessor of a property.</summary>
        public static bool IsPropertySetter(this MethodInfo method)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));
            return method.IsSpecialName && method.Name.StartsWith("set_", StringComparison.Ordinal);
        }

        // ---- Assembly scanning ----------------------------------------------

        /// <summary>Enumerates every loadable type across all currently loaded assemblies that
        /// satisfies <paramref name="predicate"/>. Assemblies that fail to fully load are skipped
        /// gracefully (only their resolvable types are considered).</summary>
        public static IEnumerable<Type> FindTypes(Func<Type, bool> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in SafeGetTypes(assembly))
                {
                    if (type != null && predicate(type)) yield return type;
                }
            }
        }

        /// <summary>Enumerates every type across all loaded assemblies annotated with
        /// <typeparamref name="TAttribute"/>.</summary>
        public static IEnumerable<Type> FindTypesWithAttribute<TAttribute>(bool inherit = true)
            where TAttribute : Attribute
        {
            return FindTypes(t => t.IsDefined(typeof(TAttribute), inherit));
        }

        internal static Type[] SafeGetTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types; } // partially resolved set
        }
    }
}
