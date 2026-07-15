using System;
using System.Collections.Generic;

namespace PFound.LocalizationService
{
    /// <summary>
    /// Derives localization keys for enum values. The key of a value is <c>{prefix}_{valueName}</c>,
    /// where the prefix comes from a <see cref="LocalizableEnumAttribute"/> argument or, absent that,
    /// the enum type name. Engine-free (pure reflection) so it is shared by the runtime value wrapper
    /// and the editor auto-generator.
    /// </summary>
    public static class EnumKeyGenerator
    {
        public static string PrefixFor(Type enumType)
        {
            if (enumType == null) throw new ArgumentNullException(nameof(enumType));
            var attr = (LocalizableEnumAttribute)Attribute.GetCustomAttribute(enumType, typeof(LocalizableEnumAttribute));
            if (attr != null && !string.IsNullOrEmpty(attr.Prefix)) return attr.Prefix;
            return enumType.Name;
        }

        public static string KeyFor(Enum value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            return PrefixFor(value.GetType()) + "_" + value.ToString();
        }

        public static string KeyFor(Type enumType, string valueName)
        {
            return PrefixFor(enumType) + "_" + valueName;
        }

        /// <summary>Every <c>{prefix}_{name}</c> key for the members of <paramref name="enumType"/>.</summary>
        public static IEnumerable<string> KeysFor(Type enumType)
        {
            string prefix = PrefixFor(enumType);
            foreach (var name in Enum.GetNames(enumType))
                yield return prefix + "_" + name;
        }

        public static bool IsLocalizable(Type type)
        {
            return type != null && type.IsEnum
                && Attribute.IsDefined(type, typeof(LocalizableEnumAttribute));
        }
    }
}
