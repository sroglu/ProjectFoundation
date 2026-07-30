using System;
using System.Collections.Generic;

namespace PFound.UserPrefs
{
    public readonly struct PrefKey<T> : IEquatable<PrefKey<T>>
    {
        internal const string ReservedPrefix = "__userprefs.";

        public string Key { get; }
        public T DefaultValue { get; }
        public PrefStorage Storage { get; }
        public bool RequiresEncryption { get; }

        public PrefKey(
            string key,
            T defaultValue,
            PrefStorage storage = PrefStorage.Auto,
            bool requiresEncryption = false)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("PrefKey requires a non-null, non-whitespace key.", nameof(key));
            if (key.StartsWith(ReservedPrefix, StringComparison.Ordinal))
                throw new ArgumentException(
                    $"PrefKey key '{key}' uses the reserved '{ReservedPrefix}' prefix.",
                    nameof(key));

            Key = key;
            DefaultValue = defaultValue;
            Storage = storage;
            RequiresEncryption = requiresEncryption;
        }

        public bool Equals(PrefKey<T> other)
            => string.Equals(Key, other.Key, StringComparison.Ordinal);

        public override bool Equals(object obj)
            => obj is PrefKey<T> other && Equals(other);

        public override int GetHashCode()
            => Key != null ? StringComparer.Ordinal.GetHashCode(Key) : 0;

        public static bool operator ==(PrefKey<T> left, PrefKey<T> right) => left.Equals(right);
        public static bool operator !=(PrefKey<T> left, PrefKey<T> right) => !left.Equals(right);

        public override string ToString() => $"PrefKey<{typeof(T).Name}>(\"{Key}\")";
    }

    internal static class PrefStorageResolver
    {
        public static PrefStorage Resolve(PrefStorage requested, Type valueType)
        {
            if (requested != PrefStorage.Auto)
                return requested;

            if (valueType == typeof(bool) ||
                valueType == typeof(int) ||
                valueType == typeof(long) ||
                valueType == typeof(float) ||
                valueType == typeof(double) ||
                valueType == typeof(string) ||
                valueType.IsEnum)
                return PrefStorage.PlayerPrefs;

            return PrefStorage.JsonFile;
        }
    }
}
