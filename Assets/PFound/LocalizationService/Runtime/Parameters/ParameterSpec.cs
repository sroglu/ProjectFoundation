using System;

namespace PFound.LocalizationService
{
    /// <summary>
    /// One declared parameter of a localization entry: the tag <see cref="Name"/> it fills, an opaque
    /// <see cref="TypeName"/> (meaningful only to the consumer that injects values), and the
    /// <see cref="Format"/> intent used when rendering. Serialized as
    /// <c>TypeName-Name</c> or <c>TypeName-Name-Format</c>.
    /// </summary>
    public readonly struct ParameterSpec
    {
        public readonly string TypeName;
        public readonly string Name;
        public readonly LocalizationFormat Format;

        public ParameterSpec(string typeName, string name, LocalizationFormat format)
        {
            TypeName = typeName;
            Name = name;
            Format = format;
        }

        /// <summary>
        /// Parses a single spec of the form <c>Type-name</c> or <c>Type-name-Format</c>. The field
        /// delimiter is <see cref="LocalizationConstants.ParameterFieldDelimiter"/>. Unknown/absent
        /// format tokens fall back to <see cref="LocalizationFormat.None"/>.
        /// </summary>
        public static bool TryParse(string text, out ParameterSpec spec)
        {
            spec = default;
            if (string.IsNullOrEmpty(text)) return false;

            var fields = text.Split(LocalizationConstants.ParameterFieldDelimiter);
            if (fields.Length < 2) return false;

            string typeName = fields[0].Trim();
            string name = fields[1].Trim();
            if (typeName.Length == 0 || name.Length == 0) return false;

            var format = LocalizationFormat.None;
            if (fields.Length >= 3 && fields[2].Trim().Length > 0)
            {
                if (!Enum.TryParse(fields[2].Trim(), ignoreCase: true, out format))
                    format = LocalizationFormat.Unsupported;
            }

            spec = new ParameterSpec(typeName, name, format);
            return true;
        }

        public string Serialize()
        {
            var d = LocalizationConstants.ParameterFieldDelimiter;
            return Format == LocalizationFormat.None
                ? TypeName + d + Name
                : TypeName + d + Name + d + Format;
        }

        public override string ToString() => Serialize();
    }
}
