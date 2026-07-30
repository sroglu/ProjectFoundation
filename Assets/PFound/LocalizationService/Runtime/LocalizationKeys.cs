using System;

namespace PFound.LocalizationService
{
    /// <summary>A language identifier (e.g. "en", "tr"). Case-insensitive equality.</summary>
    public readonly struct LanguageKey : IEquatable<LanguageKey>
    {
        public readonly string Code;
        public LanguageKey(string code) { Code = code; }
        public bool IsValid => !string.IsNullOrEmpty(Code);

        public bool Equals(LanguageKey other) => string.Equals(Code, other.Code, StringComparison.OrdinalIgnoreCase);
        public override bool Equals(object obj) => obj is LanguageKey other && Equals(other);
        public override int GetHashCode() => Code != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Code) : 0;
        public override string ToString() => Code ?? "<invalid>";
    }

    /// <summary>A localization entry key. Implicitly convertible from string for ergonomic call sites.</summary>
    public readonly struct LocalizationKey : IEquatable<LocalizationKey>
    {
        public readonly string Value;
        public LocalizationKey(string value) { Value = value; }
        public bool IsValid => !string.IsNullOrEmpty(Value);

        public static implicit operator LocalizationKey(string value) => new LocalizationKey(value);

        public bool Equals(LocalizationKey other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is LocalizationKey other && Equals(other);
        public override int GetHashCode() => Value != null ? Value.GetHashCode() : 0;
        public override string ToString() => Value ?? "<invalid>";
    }

    /// <summary>
    /// Serializable authoring counterpart of <see cref="LocalizationKey"/>. The runtime key is a
    /// readonly value type Unity cannot serialize; content that AUTHORS keys in the inspector or on
    /// ScriptableObjects uses this instead. The serialized field is named <c>Key</c> so authored asset
    /// data round-trips 1:1 with a plain string key. Convert to the runtime key via <see cref="ToKey"/>
    /// or the implicit operator.
    /// </summary>
    [Serializable]
    public struct LocalizationKeyReference : IEquatable<LocalizationKeyReference>
    {
        public string Key;

        public LocalizationKeyReference(string key) { Key = key; }

        public bool IsValid => !string.IsNullOrEmpty(Key);

        /// <summary>The runtime lookup key this authored reference resolves to.</summary>
        public LocalizationKey ToKey() => new LocalizationKey(Key);

        public static implicit operator LocalizationKey(LocalizationKeyReference reference) => new LocalizationKey(reference.Key);
        public static implicit operator LocalizationKeyReference(string key) => new LocalizationKeyReference(key);

        public bool Equals(LocalizationKeyReference other) => string.Equals(Key, other.Key, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is LocalizationKeyReference other && Equals(other);
        public override int GetHashCode() => Key != null ? Key.GetHashCode() : 0;
        public override string ToString() => Key ?? "<invalid>";
    }

    /// <summary>
    /// Serializable authoring counterpart of <see cref="LanguageKey"/>. The runtime key is a
    /// readonly value type Unity cannot serialize; content that AUTHORS a language id in the inspector
    /// or on ScriptableObjects uses this instead. The serialized field is named <c>Key</c> so authored
    /// asset data round-trips 1:1 with a plain string id. Convert to the runtime key via
    /// <see cref="ToKey"/> or the implicit operator.
    /// </summary>
    [Serializable]
    public struct LanguageKeyReference : IEquatable<LanguageKeyReference>
    {
        public string Key;

        public LanguageKeyReference(string key) { Key = key; }

        public bool IsValid => !string.IsNullOrEmpty(Key);

        /// <summary>The runtime language key this authored reference resolves to.</summary>
        public LanguageKey ToKey() => new LanguageKey(Key);

        public static implicit operator LanguageKey(LanguageKeyReference reference) => new LanguageKey(reference.Key);
        public static implicit operator LanguageKeyReference(string key) => new LanguageKeyReference(key);

        public bool Equals(LanguageKeyReference other) => string.Equals(Key, other.Key, StringComparison.OrdinalIgnoreCase);
        public override bool Equals(object obj) => obj is LanguageKeyReference other && Equals(other);
        public override int GetHashCode() => Key != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Key) : 0;
        public override string ToString() => Key ?? "<invalid>";
    }
}
