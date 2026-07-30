#if UNITY_2019_1_OR_NEWER
using System.Text;
using UnityEngine;

namespace PFound.Utilities.StringTools
{
    /// <summary>
    /// Engine-facing naming helpers. Kept in a separate assembly from the pure-string core so the
    /// core stays free of UnityEngine and remains mono/csc testable.
    /// </summary>
    public static class NamingTools
    {
        private const char DefaultSeparator = '/';

        /// <summary>
        /// Builds the full scene-hierarchy path of a GameObject, from the root down to the object
        /// itself, joined with <paramref name="separator"/> (e.g. "Root/Group/Player").
        /// </summary>
        public static string FullName(this GameObject gameObject, char separator = DefaultSeparator)
        {
            if (gameObject == null) return "N/A";
            return FullName(gameObject.transform, separator);
        }

        /// <summary>
        /// Builds the full scene-hierarchy path of the component's GameObject, appending the
        /// component's type name so distinct components on the same object are distinguishable
        /// (e.g. "Root/Player (Rigidbody)").
        /// </summary>
        public static string FullName(this Component component, char separator = DefaultSeparator)
        {
            if (component == null) return "N/A";
            return FullName(component.transform, separator) + " (" + component.GetType().Name + ")";
        }

        private static string FullName(Transform transform, char separator)
        {
            if (transform.parent == null) return transform.name;

            var builder = new StringBuilder(64);
            BuildPath(transform, separator, builder);
            return builder.ToString();
        }

        private static void BuildPath(Transform transform, char separator, StringBuilder builder)
        {
            if (transform.parent != null)
            {
                BuildPath(transform.parent, separator, builder);
                builder.Append(separator);
            }
            builder.Append(transform.name);
        }
    }
}
#endif
