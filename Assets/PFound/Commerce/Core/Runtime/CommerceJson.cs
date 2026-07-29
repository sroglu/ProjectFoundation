using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PFound.Commerce.Core
{
    /// <summary>The kind of a parsed JSON node.</summary>
    public enum JsonKind
    {
        Object,
        Array,
        String,
        Number,
        Bool,
        Null
    }

    /// <summary>
    /// A minimal, allocation-simple JSON tree used to read the remote product catalog. Engine-free and
    /// dependency-free so the catalog round-trip is unit-tested under mono/csc. It is a reader only: it
    /// parses a document into typed nodes with convenient typed accessors; it does not write JSON.
    /// Malformed input yields <c>false</c> from <see cref="Parse"/> (no throw), matching the config
    /// module's all-or-nothing parse tolerance.
    /// </summary>
    public sealed class JsonValue
    {
        public JsonKind Kind { get; }

        readonly Dictionary<string, JsonValue> _object;
        readonly List<JsonValue> _array;
        readonly string _string;
        readonly double _number;
        readonly bool _bool;

        JsonValue(JsonKind kind, Dictionary<string, JsonValue> obj, List<JsonValue> arr, string str, double num, bool boolean)
        {
            Kind = kind;
            _object = obj;
            _array = arr;
            _string = str;
            _number = num;
            _bool = boolean;
        }

        internal static JsonValue NewObject(Dictionary<string, JsonValue> members) => new JsonValue(JsonKind.Object, members, null, null, 0, false);
        internal static JsonValue NewArray(List<JsonValue> items) => new JsonValue(JsonKind.Array, null, items, null, 0, false);
        internal static JsonValue NewString(string value) => new JsonValue(JsonKind.String, null, null, value, 0, false);
        internal static JsonValue NewNumber(double value) => new JsonValue(JsonKind.Number, null, null, null, value, false);
        internal static JsonValue NewBool(bool value) => new JsonValue(JsonKind.Bool, null, null, null, 0, value);
        internal static readonly JsonValue Null = new JsonValue(JsonKind.Null, null, null, null, 0, false);

        public int Count => Kind == JsonKind.Array ? _array.Count : _object.Count;
        public JsonValue At(int index) => _array[index];
        public IEnumerable<string> Keys => _object.Keys;

        public bool Has(string key) => Kind == JsonKind.Object && _object.ContainsKey(key);

        public bool TryGet(string key, out JsonValue value)
        {
            if (Kind == JsonKind.Object) return _object.TryGetValue(key, out value);
            value = null;
            return false;
        }

        public string AsString() => _string;
        public bool AsBool() => _bool;
        public int AsInt() => (int)_number;
        public long AsLong() => (long)_number;
        public double AsNumber() => _number;

        public string GetString(string key, string fallback) => TryGet(key, out JsonValue v) && v.Kind == JsonKind.String ? v._string : fallback;
        public int GetInt(string key, int fallback) => TryGet(key, out JsonValue v) && v.Kind == JsonKind.Number ? (int)v._number : fallback;
        public long GetLong(string key, long fallback) => TryGet(key, out JsonValue v) && v.Kind == JsonKind.Number ? (long)v._number : fallback;
        public bool GetBool(string key, bool fallback) => TryGet(key, out JsonValue v) && v.Kind == JsonKind.Bool ? v._bool : fallback;

        /// <summary>Parse a JSON document. Returns false on malformed input; never throws for bad input.</summary>
        public static bool Parse(string text, out JsonValue root)
        {
            root = null;
            if (string.IsNullOrEmpty(text)) return false;

            var parser = new JsonParser(text);
            if (!parser.TryParseValue(out JsonValue value)) return false;
            parser.SkipWhitespace();
            if (!parser.AtEnd) return false;

            root = value;
            return true;
        }
    }

    /// <summary>A small recursive-descent JSON parser feeding <see cref="JsonValue"/>.</summary>
    internal sealed class JsonParser
    {
        readonly string _text;
        int _pos;

        public JsonParser(string text)
        {
            _text = text;
            _pos = 0;
        }

        public bool AtEnd => _pos >= _text.Length;

        public void SkipWhitespace()
        {
            while (_pos < _text.Length)
            {
                char c = _text[_pos];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r') _pos++;
                else break;
            }
        }

        public bool TryParseValue(out JsonValue value)
        {
            value = null;
            SkipWhitespace();
            if (AtEnd) return false;

            char c = _text[_pos];
            switch (c)
            {
                case '{': return TryParseObject(out value);
                case '[': return TryParseArray(out value);
                case '"': return TryParseString(out value);
                case 't':
                case 'f': return TryParseBool(out value);
                case 'n': return TryParseNull(out value);
                default: return TryParseNumber(out value);
            }
        }

        bool TryParseObject(out JsonValue value)
        {
            value = null;
            _pos++; // consume '{'
            var members = new Dictionary<string, JsonValue>();
            SkipWhitespace();
            if (!AtEnd && _text[_pos] == '}') { _pos++; value = JsonValue.NewObject(members); return true; }

            while (true)
            {
                SkipWhitespace();
                if (AtEnd || _text[_pos] != '"') return false;
                if (!TryParseString(out JsonValue key)) return false;
                SkipWhitespace();
                if (AtEnd || _text[_pos] != ':') return false;
                _pos++; // consume ':'
                if (!TryParseValue(out JsonValue member)) return false;
                members[key.AsString()] = member;

                SkipWhitespace();
                if (AtEnd) return false;
                char c = _text[_pos++];
                if (c == ',') continue;
                if (c == '}') { value = JsonValue.NewObject(members); return true; }
                return false;
            }
        }

        bool TryParseArray(out JsonValue value)
        {
            value = null;
            _pos++; // consume '['
            var items = new List<JsonValue>();
            SkipWhitespace();
            if (!AtEnd && _text[_pos] == ']') { _pos++; value = JsonValue.NewArray(items); return true; }

            while (true)
            {
                if (!TryParseValue(out JsonValue item)) return false;
                items.Add(item);

                SkipWhitespace();
                if (AtEnd) return false;
                char c = _text[_pos++];
                if (c == ',') continue;
                if (c == ']') { value = JsonValue.NewArray(items); return true; }
                return false;
            }
        }

        bool TryParseString(out JsonValue value)
        {
            value = null;
            if (_text[_pos] != '"') return false;
            _pos++; // consume opening quote
            var builder = new StringBuilder();

            while (_pos < _text.Length)
            {
                char c = _text[_pos++];
                if (c == '"') { value = JsonValue.NewString(builder.ToString()); return true; }
                if (c == '\\')
                {
                    if (_pos >= _text.Length) return false;
                    char esc = _text[_pos++];
                    switch (esc)
                    {
                        case '"': builder.Append('"'); break;
                        case '\\': builder.Append('\\'); break;
                        case '/': builder.Append('/'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u':
                            if (_pos + 4 > _text.Length) return false;
                            string hex = _text.Substring(_pos, 4);
                            if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code)) return false;
                            builder.Append((char)code);
                            _pos += 4;
                            break;
                        default: return false;
                    }
                }
                else
                {
                    builder.Append(c);
                }
            }
            return false; // unterminated string
        }

        bool TryParseNumber(out JsonValue value)
        {
            value = null;
            int start = _pos;
            while (_pos < _text.Length)
            {
                char c = _text[_pos];
                if ((c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E') _pos++;
                else break;
            }
            if (_pos == start) return false;
            string token = _text.Substring(start, _pos - start);
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)) return false;
            value = JsonValue.NewNumber(number);
            return true;
        }

        bool TryParseBool(out JsonValue value)
        {
            value = null;
            if (Matches("true")) { _pos += 4; value = JsonValue.NewBool(true); return true; }
            if (Matches("false")) { _pos += 5; value = JsonValue.NewBool(false); return true; }
            return false;
        }

        bool TryParseNull(out JsonValue value)
        {
            value = null;
            if (Matches("null")) { _pos += 4; value = JsonValue.Null; return true; }
            return false;
        }

        bool Matches(string literal)
        {
            if (_pos + literal.Length > _text.Length) return false;
            for (int i = 0; i < literal.Length; i++)
            {
                if (_text[_pos + i] != literal[i]) return false;
            }
            return true;
        }
    }
}
