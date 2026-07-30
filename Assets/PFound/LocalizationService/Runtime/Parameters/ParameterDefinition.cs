using System.Collections.Generic;
using System.Text;

namespace PFound.LocalizationService
{
    /// <summary>
    /// The ordered parameter list of a localization entry. Serialized as a
    /// <see cref="LocalizationConstants.ParameterListDelimiter"/>-separated list of
    /// <see cref="ParameterSpec"/> specs. An empty definition means the entry takes no parameters.
    /// Look-up of a spec by tag name is ordinal / case-sensitive.
    /// </summary>
    public sealed class ParameterDefinition
    {
        public static readonly ParameterDefinition Empty = new ParameterDefinition(new ParameterSpec[0]);

        private readonly ParameterSpec[] _specs;

        public ParameterDefinition(ParameterSpec[] specs)
        {
            _specs = specs ?? new ParameterSpec[0];
        }

        public int Count => _specs.Length;
        public ParameterSpec this[int index] => _specs[index];

        /// <summary>Index of the spec whose name matches <paramref name="name"/> (ordinal), or -1.</summary>
        public int IndexOf(string name)
        {
            for (int i = 0; i < _specs.Length; i++)
                if (string.Equals(_specs[i].Name, name, System.StringComparison.Ordinal))
                    return i;
            return -1;
        }

        public bool TryGet(string name, out ParameterSpec spec)
        {
            int i = IndexOf(name);
            if (i >= 0) { spec = _specs[i]; return true; }
            spec = default;
            return false;
        }

        /// <summary>
        /// Parses a definition cell. Empty / whitespace yields <see cref="Empty"/>. Individual specs that
        /// fail to parse are skipped (and reported).
        /// </summary>
        public static ParameterDefinition Parse(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0)
                return Empty;

            var parts = text.Split(LocalizationConstants.ParameterListDelimiter);
            var list = new List<ParameterSpec>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (part.Length == 0) continue;
                if (ParameterSpec.TryParse(part, out var spec)) list.Add(spec);
                else LocalizationLog.Warn("Ignoring malformed parameter spec: '" + part + "'");
            }
            return list.Count == 0 ? Empty : new ParameterDefinition(list.ToArray());
        }

        public string Serialize()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _specs.Length; i++)
            {
                if (i > 0) sb.Append(LocalizationConstants.ParameterListDelimiter);
                sb.Append(_specs[i].Serialize());
            }
            return sb.ToString();
        }

        public override string ToString() => Serialize();
    }
}
