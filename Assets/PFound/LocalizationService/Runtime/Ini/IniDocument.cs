using System.Collections.Generic;
using System.Text;

namespace PFound.LocalizationService
{
    /// <summary>
    /// Minimal sectioned key/value document used by the localization table files. A section starts with
    /// a <c>[name]</c> header line and runs until the next header. Within a section each non-empty line
    /// is a <c>key=value</c> pair split on the first <c>=</c>. Keys and values carry no surrounding
    /// whitespace and there are no blank lines. On write, runtime newlines inside a value are escaped to
    /// the stored two-character form and reversed on read.
    /// </summary>
    public sealed class IniDocument
    {
        // Insertion-ordered sections; each section keeps insertion-ordered entries.
        private readonly List<string> _sectionOrder = new List<string>();
        private readonly Dictionary<string, OrderedMap> _sections = new Dictionary<string, OrderedMap>();

        public IReadOnlyList<string> SectionNames => _sectionOrder;

        public bool HasSection(string name) => _sections.ContainsKey(name);

        public OrderedMap Section(string name)
        {
            if (!_sections.TryGetValue(name, out var map))
            {
                map = new OrderedMap();
                _sections[name] = map;
                _sectionOrder.Add(name);
            }
            return map;
        }

        public bool TryGetSection(string name, out OrderedMap map) => _sections.TryGetValue(name, out map);

        /// <summary>Parses document text. Header lines are <c>[name]</c>; entries are <c>key=value</c>.</summary>
        public static IniDocument Parse(string text)
        {
            var doc = new IniDocument();
            if (string.IsNullOrEmpty(text)) return doc;

            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            OrderedMap current = null;
            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0) continue;
                if (line[0] == '[' && line[line.Length - 1] == ']')
                {
                    string name = line.Substring(1, line.Length - 2).Trim();
                    current = doc.Section(name);
                    continue;
                }
                if (current == null) continue; // entries before any header are ignored
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                string key = line.Substring(0, eq).Trim();
                string value = Unescape(line.Substring(eq + 1).Trim());
                if (key.Length > 0) current[key] = value;
            }
            return doc;
        }

        public string Write()
        {
            var sb = new StringBuilder();
            foreach (var section in _sectionOrder)
            {
                sb.Append('[').Append(section).Append("]\n");
                foreach (var pair in _sections[section])
                    sb.Append(pair.Key).Append('=').Append(Escape(pair.Value)).Append('\n');
            }
            return sb.ToString();
        }

        private static string Escape(string value)
            => value == null ? string.Empty
             : value.Replace(LocalizationConstants.RuntimeNewline, LocalizationConstants.StoredNewline);

        private static string Unescape(string value)
            => value == null ? string.Empty
             : value.Replace(LocalizationConstants.StoredNewline, LocalizationConstants.RuntimeNewline);

        /// <summary>Insertion-ordered string map; enough for the table files without extra deps.</summary>
        public sealed class OrderedMap : IEnumerable<KeyValuePair<string, string>>
        {
            private readonly List<string> _order = new List<string>();
            private readonly Dictionary<string, string> _map = new Dictionary<string, string>();

            public int Count => _order.Count;
            public IReadOnlyList<string> Keys => _order;

            public string this[string key]
            {
                get => _map[key];
                set { if (!_map.ContainsKey(key)) _order.Add(key); _map[key] = value; }
            }

            public bool TryGetValue(string key, out string value) => _map.TryGetValue(key, out value);
            public bool ContainsKey(string key) => _map.ContainsKey(key);

            public IReadOnlyDictionary<string, string> AsReadOnly() => _map;

            public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
            {
                foreach (var key in _order) yield return new KeyValuePair<string, string>(key, _map[key]);
            }
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
