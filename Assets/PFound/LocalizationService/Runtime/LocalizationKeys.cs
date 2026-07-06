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
}
