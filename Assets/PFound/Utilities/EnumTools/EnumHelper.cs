using System;

namespace PFound.Utilities.EnumTools
{
    /// <summary>Named pairing of an enum member's underlying integer value and its declared name.</summary>
    public readonly struct EnumEntry<TEnum> where TEnum : struct, Enum
    {
        public readonly TEnum Value;
        public readonly int IntValue;
        public readonly string Name;

        public EnumEntry(TEnum value, int intValue, string name)
        {
            Value = value;
            IntValue = intValue;
            Name = name;
        }
    }

    /// <summary>
    /// Enum conveniences that are not trivially expressible with the BCL alone.
    /// </summary>
    public static class EnumHelper
    {
        /// <summary>
        /// Parses <paramref name="text"/> into <typeparamref name="TEnum"/>, returning
        /// <paramref name="fallback"/> when the text is null/blank or does not name a member.
        /// </summary>
        public static TEnum ParseOrDefault<TEnum>(string text, TEnum fallback = default, bool ignoreCase = true)
            where TEnum : struct, Enum
        {
            if (string.IsNullOrWhiteSpace(text))
                return fallback;
            return Enum.TryParse<TEnum>(text, ignoreCase, out var parsed) ? parsed : fallback;
        }

        /// <summary>Tries to parse <paramref name="text"/>; on failure yields <paramref name="fallback"/> via <paramref name="result"/>.</summary>
        public static bool TryParse<TEnum>(string text, TEnum fallback, out TEnum result, bool ignoreCase = true)
            where TEnum : struct, Enum
        {
            if (!string.IsNullOrWhiteSpace(text) && Enum.TryParse<TEnum>(text, ignoreCase, out result))
                return true;
            result = fallback;
            return false;
        }

        /// <summary>Returns every declared value of the enum as a strongly typed array.</summary>
        public static TEnum[] GetValues<TEnum>() where TEnum : struct, Enum
        {
            return (TEnum[])Enum.GetValues(typeof(TEnum));
        }

        /// <summary>Returns the declared names of the enum.</summary>
        public static string[] GetNames<TEnum>() where TEnum : struct, Enum
        {
            return Enum.GetNames(typeof(TEnum));
        }

        /// <summary>Returns each declared member paired with its underlying integer value and name.</summary>
        public static EnumEntry<TEnum>[] GetEntries<TEnum>() where TEnum : struct, Enum
        {
            var values = GetValues<TEnum>();
            var entries = new EnumEntry<TEnum>[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                var value = values[i];
                entries[i] = new EnumEntry<TEnum>(
                    value,
                    Convert.ToInt32(value),
                    value.ToString());
            }
            return entries;
        }

        /// <summary>True when <paramref name="value"/> equals any of the supplied options.</summary>
        public static bool IsAnyOf<TEnum>(this TEnum value, params TEnum[] options)
            where TEnum : struct, Enum
        {
            if (options == null)
                return false;
            for (int i = 0; i < options.Length; i++)
            {
                if (EqualityComparerEquals(value, options[i]))
                    return true;
            }
            return false;
        }

        /// <summary>True when <paramref name="value"/> is a member declared by <typeparamref name="TEnum"/>.</summary>
        public static bool IsDefined<TEnum>(TEnum value) where TEnum : struct, Enum
        {
            return Enum.IsDefined(typeof(TEnum), value);
        }

        /// <summary>True when the integer <paramref name="value"/> corresponds to a declared member of <typeparamref name="TEnum"/>.</summary>
        public static bool IsDefined<TEnum>(int value) where TEnum : struct, Enum
        {
            return Enum.IsDefined(typeof(TEnum), value);
        }

        private static bool EqualityComparerEquals<TEnum>(TEnum a, TEnum b) where TEnum : struct, Enum
        {
            return System.Collections.Generic.EqualityComparer<TEnum>.Default.Equals(a, b);
        }
    }
}
