using System;
using System.Globalization;

namespace PFound.Utilities.ApplicationTools
{
    /// <summary>
    /// An immutable semantic-style application version made of Major, Minor and
    /// Patch numbers. Ordered and comparable; parses and renders the canonical
    /// <c>Major.Minor.Patch</c> form (a missing component parses as zero).
    /// </summary>
    public readonly struct ApplicationVersion
        : IEquatable<ApplicationVersion>, IComparable<ApplicationVersion>
    {
        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }

        public ApplicationVersion(int major, int minor = 0, int patch = 0)
        {
            if (major < 0) throw new ArgumentOutOfRangeException(nameof(major));
            if (minor < 0) throw new ArgumentOutOfRangeException(nameof(minor));
            if (patch < 0) throw new ArgumentOutOfRangeException(nameof(patch));

            Major = major;
            Minor = minor;
            Patch = patch;
        }

        /// <summary>
        /// Parses a dotted version string such as <c>"1"</c>, <c>"1.4"</c> or
        /// <c>"1.4.2"</c>. Throws <see cref="FormatException"/> on invalid input.
        /// </summary>
        public static ApplicationVersion Parse(string text)
        {
            if (!TryParse(text, out ApplicationVersion version))
                throw new FormatException($"'{text}' is not a valid application version.");
            return version;
        }

        /// <summary>Attempts to parse a dotted version string.</summary>
        public static bool TryParse(string text, out ApplicationVersion version)
        {
            version = default;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string[] parts = text.Trim().Split('.');
            if (parts.Length == 0 || parts.Length > 3)
                return false;

            int[] numbers = new int[3];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out int n)
                    || n < 0)
                {
                    return false;
                }
                numbers[i] = n;
            }

            version = new ApplicationVersion(numbers[0], numbers[1], numbers[2]);
            return true;
        }

        public int CompareTo(ApplicationVersion other)
        {
            int cmp = Major.CompareTo(other.Major);
            if (cmp != 0) return cmp;
            cmp = Minor.CompareTo(other.Minor);
            if (cmp != 0) return cmp;
            return Patch.CompareTo(other.Patch);
        }

        public bool Equals(ApplicationVersion other)
        {
            return Major == other.Major && Minor == other.Minor && Patch == other.Patch;
        }

        public override bool Equals(object obj)
        {
            return obj is ApplicationVersion other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + Major;
                hash = hash * 31 + Minor;
                hash = hash * 31 + Patch;
                return hash;
            }
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}", Major, Minor, Patch);
        }

        public static bool operator ==(ApplicationVersion left, ApplicationVersion right) => left.Equals(right);
        public static bool operator !=(ApplicationVersion left, ApplicationVersion right) => !left.Equals(right);
        public static bool operator <(ApplicationVersion left, ApplicationVersion right) => left.CompareTo(right) < 0;
        public static bool operator >(ApplicationVersion left, ApplicationVersion right) => left.CompareTo(right) > 0;
        public static bool operator <=(ApplicationVersion left, ApplicationVersion right) => left.CompareTo(right) <= 0;
        public static bool operator >=(ApplicationVersion left, ApplicationVersion right) => left.CompareTo(right) >= 0;
    }
}
