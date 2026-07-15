using System;

namespace PFound.LocalizationService
{
    /// <summary>
    /// Marks an enum whose values participate in localization. Each value gets an auto key
    /// <c>{Prefix}_{ValueName}</c>. <see cref="Prefix"/> defaults to the enum type name when not given.
    /// The editor scanner discovers these to auto-generate content keys.
    /// </summary>
    [AttributeUsage(AttributeTargets.Enum, Inherited = false, AllowMultiple = false)]
    public sealed class LocalizableEnumAttribute : Attribute
    {
        /// <summary>Explicit key prefix, or null to use the enum type name.</summary>
        public string Prefix { get; }

        public LocalizableEnumAttribute() { Prefix = null; }
        public LocalizableEnumAttribute(string prefix) { Prefix = prefix; }
    }
}
