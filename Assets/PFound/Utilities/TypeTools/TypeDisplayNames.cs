using System;
using System.Collections.Concurrent;
using System.Text;

namespace PFound.Utilities.TypeTools
{
    /// <summary>
    /// Produces human-readable display names for <see cref="Type"/> values — e.g.
    /// <c>Dictionary&lt;String, List&lt;Int32&gt;&gt;</c> instead of the mangled runtime name
    /// <c>Dictionary`2[...]</c> — and caches the result per type. Engine-free and thread-safe.
    /// </summary>
    public static class TypeDisplayNames
    {
        private static readonly ConcurrentDictionary<Type, string> Cache =
            new ConcurrentDictionary<Type, string>();

        /// <summary>Returns a cached, reader-friendly name for <paramref name="type"/>. Generic
        /// arguments are expanded recursively; arrays keep their rank brackets.</summary>
        public static string DisplayNameOf(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            return Cache.GetOrAdd(type, Compose);
        }

        /// <summary>Removes every cached entry.</summary>
        public static void Clear() => Cache.Clear();

        private static string Compose(Type type)
        {
            if (type.IsArray)
            {
                var rank = type.GetArrayRank();
                var commas = rank > 1 ? new string(',', rank - 1) : string.Empty;
                return Compose(type.GetElementType()) + "[" + commas + "]";
            }

            if (type.IsGenericType)
            {
                var builder = new StringBuilder();
                var name = type.Name;
                var tick = name.IndexOf('`');
                builder.Append(tick >= 0 ? name.Substring(0, tick) : name);
                builder.Append('<');

                var args = type.GetGenericArguments();
                for (var i = 0; i < args.Length; i++)
                {
                    if (i > 0) builder.Append(", ");
                    builder.Append(Compose(args[i]));
                }
                builder.Append('>');
                return builder.ToString();
            }

            return type.Name;
        }
    }
}
