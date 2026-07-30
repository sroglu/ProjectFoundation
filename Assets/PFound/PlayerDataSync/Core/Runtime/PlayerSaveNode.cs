using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PFound.PlayerDataSync.Core
{
    enum SaveNodeKind
    {
        Object,
        Array,
        String,
        Number,
        Bool,
        Null
    }

    /// <summary>
    /// A small, engine-free, mutable document tree (object / array / string / number / bool / null) that
    /// models a player save as backend-neutral data. It is the in-memory form of the opaque blob: the game
    /// maps its own typed model onto this tree, migrations rewrite it in place, and the codec serializes it
    /// to a single self-contained JSON string that any backend stores verbatim. Serialization is
    /// deterministic (object keys emitted in ordinal order) so the same logical save always produces the
    /// same blob string — which is what lets one blob move between backends unchanged and lets tests compare
    /// blobs by value. Unknown members are preserved across a parse/serialize round-trip, giving forward
    /// compatibility: an older client reading a document written by a newer one keeps the fields it does not
    /// understand instead of dropping them.
    /// </summary>
    public sealed class PlayerSaveNode
    {
        SaveNodeKind _kind;
        Dictionary<string, PlayerSaveNode> _members;
        List<PlayerSaveNode> _items;
        string _text;
        double _number;
        bool _bool;

        PlayerSaveNode(SaveNodeKind kind) { _kind = kind; }

        public static PlayerSaveNode NewObject() => new PlayerSaveNode(SaveNodeKind.Object) { _members = new Dictionary<string, PlayerSaveNode>() };
        public static PlayerSaveNode NewArray() => new PlayerSaveNode(SaveNodeKind.Array) { _items = new List<PlayerSaveNode>() };
        public static PlayerSaveNode Null() => new PlayerSaveNode(SaveNodeKind.Null);
        public static PlayerSaveNode Of(string value) => value == null ? Null() : new PlayerSaveNode(SaveNodeKind.String) { _text = value };
        public static PlayerSaveNode Of(bool value) => new PlayerSaveNode(SaveNodeKind.Bool) { _bool = value };
        public static PlayerSaveNode Of(long value) => new PlayerSaveNode(SaveNodeKind.Number) { _number = value };
        public static PlayerSaveNode Of(double value) => new PlayerSaveNode(SaveNodeKind.Number) { _number = value };

        public bool IsObject => _kind == SaveNodeKind.Object;
        public bool IsArray => _kind == SaveNodeKind.Array;
        public bool IsNull => _kind == SaveNodeKind.Null;

        // --- scalar reads (with cross-type conversion, so a stored "5" reads as int/long/double/bool) ---

        public string AsString(string fallback = "")
        {
            switch (_kind)
            {
                case SaveNodeKind.String: return _text;
                case SaveNodeKind.Number: return FormatNumber(_number);
                case SaveNodeKind.Bool: return _bool ? "true" : "false";
                default: return fallback;
            }
        }

        public double AsDouble(double fallback = 0)
        {
            switch (_kind)
            {
                case SaveNodeKind.Number: return _number;
                case SaveNodeKind.Bool: return _bool ? 1 : 0;
                case SaveNodeKind.String:
                    return double.TryParse(_text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) ? parsed : fallback;
                default: return fallback;
            }
        }

        public long AsLong(long fallback = 0)
        {
            switch (_kind)
            {
                case SaveNodeKind.Number: return (long)_number;
                case SaveNodeKind.Bool: return _bool ? 1 : 0;
                case SaveNodeKind.String:
                    return long.TryParse(_text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) ? parsed : fallback;
                default: return fallback;
            }
        }

        public int AsInt(int fallback = 0) => (int)AsLong(fallback);

        public bool AsBool(bool fallback = false)
        {
            switch (_kind)
            {
                case SaveNodeKind.Bool: return _bool;
                case SaveNodeKind.Number: return _number != 0;
                case SaveNodeKind.String:
                    if (bool.TryParse(_text, out bool parsed)) return parsed;
                    return fallback;
                default: return fallback;
            }
        }

        // --- object membership ---

        public IEnumerable<string> Keys => _members.Keys;
        public bool Has(string key) => _members.ContainsKey(key);
        public bool TryGet(string key, out PlayerSaveNode value) => _members.TryGetValue(key, out value);
        public void Remove(string key) => _members.Remove(key);

        public PlayerSaveNode Set(string key, PlayerSaveNode value) { _members[key] = value; return value; }
        public void SetString(string key, string value) => _members[key] = Of(value);
        public void SetLong(string key, long value) => _members[key] = Of(value);
        public void SetInt(string key, int value) => _members[key] = Of((long)value);
        public void SetDouble(string key, double value) => _members[key] = Of(value);
        public void SetBool(string key, bool value) => _members[key] = Of(value);

        /// <summary>Fetch an existing object child or create+attach a fresh empty object under this key.</summary>
        public PlayerSaveNode GetOrAddObject(string key)
        {
            if (_members.TryGetValue(key, out PlayerSaveNode existing) && existing.IsObject) return existing;
            PlayerSaveNode child = NewObject();
            _members[key] = child;
            return child;
        }

        public string GetString(string key, string fallback = "") => _members.TryGetValue(key, out PlayerSaveNode n) ? n.AsString(fallback) : fallback;
        public long GetLong(string key, long fallback = 0) => _members.TryGetValue(key, out PlayerSaveNode n) ? n.AsLong(fallback) : fallback;
        public int GetInt(string key, int fallback = 0) => _members.TryGetValue(key, out PlayerSaveNode n) ? n.AsInt(fallback) : fallback;
        public double GetDouble(string key, double fallback = 0) => _members.TryGetValue(key, out PlayerSaveNode n) ? n.AsDouble(fallback) : fallback;
        public bool GetBool(string key, bool fallback = false) => _members.TryGetValue(key, out PlayerSaveNode n) ? n.AsBool(fallback) : fallback;

        // --- array membership ---

        public int Count => _items.Count;
        public PlayerSaveNode Item(int index) => _items[index];
        public void Add(PlayerSaveNode value) => _items.Add(value);

        // --- serialization ---

        public string ToJson()
        {
            var builder = new StringBuilder();
            Write(builder);
            return builder.ToString();
        }

        void Write(StringBuilder builder)
        {
            switch (_kind)
            {
                case SaveNodeKind.Object:
                    builder.Append('{');
                    bool firstMember = true;
                    // Ordinal key order makes the serialized form canonical and reproducible.
                    foreach (string key in _members.Keys.OrderBy(k => k, StringComparer.Ordinal))
                    {
                        if (!firstMember) builder.Append(',');
                        firstMember = false;
                        WriteString(builder, key);
                        builder.Append(':');
                        _members[key].Write(builder);
                    }
                    builder.Append('}');
                    break;
                case SaveNodeKind.Array:
                    builder.Append('[');
                    for (int i = 0; i < _items.Count; i++)
                    {
                        if (i > 0) builder.Append(',');
                        _items[i].Write(builder);
                    }
                    builder.Append(']');
                    break;
                case SaveNodeKind.String:
                    WriteString(builder, _text);
                    break;
                case SaveNodeKind.Number:
                    builder.Append(FormatNumber(_number));
                    break;
                case SaveNodeKind.Bool:
                    builder.Append(_bool ? "true" : "false");
                    break;
                default:
                    builder.Append("null");
                    break;
            }
        }

        static string FormatNumber(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return "0";
            // Integral magnitudes within long range serialize without a decimal point so ints round-trip exactly.
            if (value == Math.Floor(value) && Math.Abs(value) < 9.007199254740992e15)
            {
                return ((long)value).ToString(CultureInfo.InvariantCulture);
            }
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        static void WriteString(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < 0x20) builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else builder.Append(c);
                        break;
                }
            }
            builder.Append('"');
        }

        public static PlayerSaveNode Parse(string text)
        {
            int index = 0;
            PlayerSaveNode value = ParseValue(text, ref index);
            SkipWhitespace(text, ref index);
            if (index != text.Length) throw new PlayerSaveFormatException("Trailing characters after JSON value.");
            return value;
        }

        static PlayerSaveNode ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length) throw new PlayerSaveFormatException("Unexpected end of input.");
            char c = s[i];
            switch (c)
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return Of(ParseString(s, ref i));
                case 't':
                case 'f': return Of(ParseBool(s, ref i));
                case 'n': ParseLiteral(s, ref i, "null"); return Null();
                default: return Of(ParseNumber(s, ref i));
            }
        }

        static PlayerSaveNode ParseObject(string s, ref int i)
        {
            PlayerSaveNode node = NewObject();
            i++;
            SkipWhitespace(s, ref i);
            if (Peek(s, i) == '}') { i++; return node; }
            while (true)
            {
                SkipWhitespace(s, ref i);
                if (Peek(s, i) != '"') throw new PlayerSaveFormatException("Expected object key.");
                string key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);
                if (Peek(s, i) != ':') throw new PlayerSaveFormatException("Expected ':' after object key.");
                i++;
                node._members[key] = ParseValue(s, ref i);
                SkipWhitespace(s, ref i);
                char next = Peek(s, i);
                if (next == ',') { i++; continue; }
                if (next == '}') { i++; break; }
                throw new PlayerSaveFormatException("Expected ',' or '}' in object.");
            }
            return node;
        }

        static PlayerSaveNode ParseArray(string s, ref int i)
        {
            PlayerSaveNode node = NewArray();
            i++;
            SkipWhitespace(s, ref i);
            if (Peek(s, i) == ']') { i++; return node; }
            while (true)
            {
                node._items.Add(ParseValue(s, ref i));
                SkipWhitespace(s, ref i);
                char next = Peek(s, i);
                if (next == ',') { i++; continue; }
                if (next == ']') { i++; break; }
                throw new PlayerSaveFormatException("Expected ',' or ']' in array.");
            }
            return node;
        }

        static string ParseString(string s, ref int i)
        {
            i++;
            var sb = new StringBuilder();
            while (true)
            {
                if (i >= s.Length) throw new PlayerSaveFormatException("Unterminated string.");
                char c = s[i++];
                if (c == '"') break;
                if (c == '\\')
                {
                    if (i >= s.Length) throw new PlayerSaveFormatException("Unterminated escape.");
                    char e = s[i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 > s.Length) throw new PlayerSaveFormatException("Truncated unicode escape.");
                            int code = int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                            sb.Append((char)code);
                            i += 4;
                            break;
                        default: throw new PlayerSaveFormatException("Invalid escape character.");
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        static double ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length)
            {
                char c = s[i];
                bool numeric = (c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E';
                if (!numeric) break;
                i++;
            }
            if (i == start) throw new PlayerSaveFormatException("Expected a value.");
            string token = s.Substring(start, i - start);
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                throw new PlayerSaveFormatException("Malformed number.");
            }
            return value;
        }

        static bool ParseBool(string s, ref int i)
        {
            if (Peek(s, i) == 't') { ParseLiteral(s, ref i, "true"); return true; }
            ParseLiteral(s, ref i, "false");
            return false;
        }

        static void ParseLiteral(string s, ref int i, string literal)
        {
            if (i + literal.Length > s.Length || s.Substring(i, literal.Length) != literal)
            {
                throw new PlayerSaveFormatException("Invalid literal.");
            }
            i += literal.Length;
        }

        static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length)
            {
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r') i++;
                else break;
            }
        }

        static char Peek(string s, int i) => i < s.Length ? s[i] : '\0';
    }

    /// <summary>Raised when a stored blob is not well-formed. Callers at the persistence boundary translate it into a recoverable state (keep the last good local save) rather than crashing the game.</summary>
    public sealed class PlayerSaveFormatException : Exception
    {
        public PlayerSaveFormatException(string message) : base(message) { }
    }
}
