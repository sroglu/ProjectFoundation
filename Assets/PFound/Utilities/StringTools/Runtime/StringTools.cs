using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PFound.Utilities.StringTools
{
    /// <summary>
    /// Engine-free plain-string helpers built on <see cref="System.String"/> and
    /// <see cref="System.Text.StringBuilder"/>. No third-party string builder coupling so the
    /// assembly stays mono/csc testable without Unity.
    /// </summary>
    public static partial class StringTools
    {
        // ------------------------------------------------------------------ Extraction

        /// <summary>
        /// Returns the text located between the first occurrence of <paramref name="start"/> and
        /// the first occurrence of <paramref name="end"/> that follows it. The markers themselves
        /// are excluded. Returns <c>null</c> when either marker is missing.
        /// </summary>
        public static string Between(this string text, string start, string end,
            StringComparison comparison = StringComparison.Ordinal)
        {
            if (text == null || start == null || end == null) return null;

            var startIndex = text.IndexOf(start, comparison);
            if (startIndex < 0) return null;
            var contentStart = startIndex + start.Length;

            var endIndex = text.IndexOf(end, contentStart, comparison);
            if (endIndex < 0) return null;

            return text.Substring(contentStart, endIndex - contentStart);
        }

        // ------------------------------------------------------------------ Replacement

        /// <summary>Replaces only the first occurrence of <paramref name="oldValue"/>.</summary>
        public static string ReplaceFirstOccurrence(this string text, string oldValue, string newValue,
            StringComparison comparison = StringComparison.Ordinal)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(oldValue)) return text;

            var index = text.IndexOf(oldValue, comparison);
            if (index < 0) return text;

            return text.Substring(0, index) + newValue + text.Substring(index + oldValue.Length);
        }

        /// <summary>Replaces only the last occurrence of <paramref name="oldValue"/>.</summary>
        public static string ReplaceLastOccurrence(this string text, string oldValue, string newValue,
            StringComparison comparison = StringComparison.Ordinal)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(oldValue)) return text;

            var index = text.LastIndexOf(oldValue, comparison);
            if (index < 0) return text;

            return text.Substring(0, index) + newValue + text.Substring(index + oldValue.Length);
        }

        /// <summary>
        /// Replaces the content between the first <paramref name="start"/>/<paramref name="end"/>
        /// marker pair with <paramref name="replacement"/>. The markers are kept by default.
        /// </summary>
        public static string ReplaceInside(this string text, string start, string end, string replacement,
            bool keepMarkers = true, StringComparison comparison = StringComparison.Ordinal)
        {
            if (text == null || start == null || end == null) return text;

            var startIndex = text.IndexOf(start, comparison);
            if (startIndex < 0) return text;
            var contentStart = startIndex + start.Length;

            var endIndex = text.IndexOf(end, contentStart, comparison);
            if (endIndex < 0) return text;

            if (keepMarkers)
            {
                return text.Substring(0, contentStart) + replacement + text.Substring(endIndex);
            }

            return text.Substring(0, startIndex) + replacement + text.Substring(endIndex + end.Length);
        }

        /// <summary>
        /// Repeatedly applies the between-marker replacement until no more marker pairs remain,
        /// replacing every non-overlapping region between the markers.
        /// </summary>
        public static string ReplaceAllInside(this string text, string start, string end, string replacement,
            bool keepMarkers = true, StringComparison comparison = StringComparison.Ordinal)
        {
            if (text == null || string.IsNullOrEmpty(start) || string.IsNullOrEmpty(end)) return text;

            var builder = new StringBuilder(text.Length);
            var cursor = 0;
            while (true)
            {
                var startIndex = text.IndexOf(start, cursor, comparison);
                if (startIndex < 0) break;
                var contentStart = startIndex + start.Length;

                var endIndex = text.IndexOf(end, contentStart, comparison);
                if (endIndex < 0) break;

                if (keepMarkers)
                {
                    builder.Append(text, cursor, contentStart - cursor);
                    builder.Append(replacement);
                    builder.Append(end);
                }
                else
                {
                    builder.Append(text, cursor, startIndex - cursor);
                    builder.Append(replacement);
                }

                cursor = endIndex + end.Length;
            }

            builder.Append(text, cursor, text.Length - cursor);
            return builder.ToString();
        }

        // ------------------------------------------------------------------ Building / trimming

        /// <summary>Concatenates <paramref name="text"/> with itself <paramref name="count"/> times.</summary>
        public static string Repeat(this string text, int count)
        {
            if (count <= 0 || string.IsNullOrEmpty(text)) return string.Empty;
            if (count == 1) return text;

            var builder = new StringBuilder(text.Length * count);
            for (var i = 0; i < count; i++) builder.Append(text);
            return builder.ToString();
        }

        /// <summary>Removes every leading run of <paramref name="trimString"/>.</summary>
        public static string TrimStart(this string text, string trimString,
            StringComparison comparison = StringComparison.Ordinal)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(trimString)) return text;

            var start = 0;
            while (start + trimString.Length <= text.Length &&
                   string.Compare(text, start, trimString, 0, trimString.Length, comparison) == 0)
            {
                start += trimString.Length;
            }

            return start == 0 ? text : text.Substring(start);
        }

        /// <summary>Removes every trailing run of <paramref name="trimString"/>.</summary>
        public static string TrimEnd(this string text, string trimString,
            StringComparison comparison = StringComparison.Ordinal)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(trimString)) return text;

            var end = text.Length;
            while (end - trimString.Length >= 0 &&
                   string.Compare(text, end - trimString.Length, trimString, 0, trimString.Length, comparison) == 0)
            {
                end -= trimString.Length;
            }

            return end == text.Length ? text : text.Substring(0, end);
        }

        // ------------------------------------------------------------------ Counting

        /// <summary>Counts occurrences of a single character.</summary>
        public static int CountCharacters(this string text, char character)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            var count = 0;
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == character) count++;
            }
            return count;
        }

        /// <summary>Counts non-overlapping occurrences of <paramref name="substring"/>.</summary>
        public static int CountSubstrings(this string text, string substring,
            StringComparison comparison = StringComparison.Ordinal)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(substring)) return 0;

            var count = 0;
            var index = 0;
            while ((index = text.IndexOf(substring, index, comparison)) >= 0)
            {
                count++;
                index += substring.Length;
            }
            return count;
        }

        // ------------------------------------------------------------------ Classification

        /// <summary>
        /// True when the text is a non-empty run of ASCII decimal digits, optionally prefixed by a
        /// single '+' or '-' sign.
        /// </summary>
        public static bool IsNumeric(this string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            var start = 0;
            if (text[0] == '+' || text[0] == '-') start = 1;
            if (start == text.Length) return false;

            for (var i = start; i < text.Length; i++)
            {
                if (text[i] < '0' || text[i] > '9') return false;
            }
            return true;
        }

        /// <summary>True when the text parses as a floating point number using invariant culture.</summary>
        public static bool IsNumericFloat(this string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out _);
        }

        /// <summary>True when every character is an ASCII letter or digit (non-empty).</summary>
        public static bool IsAsciiLettersOrDigits(this string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                var ok = (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
                if (!ok) return false;
            }
            return true;
        }

        /// <summary>True when every character is a Unicode letter or digit (non-empty).</summary>
        public static bool IsLettersOrDigits(this string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            for (var i = 0; i < text.Length; i++)
            {
                if (!char.IsLetterOrDigit(text[i])) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ Clipping

        /// <summary>
        /// Clips <paramref name="text"/> so the result never exceeds <paramref name="maximumLength"/>
        /// characters. When clipping occurs and an <paramref name="overflowSuffix"/> is supplied, the
        /// suffix is appended within the budget (content is shortened to make room).
        /// </summary>
        public static string Truncate(this string text, int maximumLength, string overflowSuffix = null)
        {
            if (maximumLength < 0) throw new ArgumentOutOfRangeException(nameof(maximumLength));
            if (string.IsNullOrEmpty(text) || text.Length <= maximumLength) return text;

            if (string.IsNullOrEmpty(overflowSuffix))
            {
                return text.Substring(0, maximumLength);
            }

            if (overflowSuffix.Length >= maximumLength)
            {
                return overflowSuffix.Substring(0, maximumLength);
            }

            return text.Substring(0, maximumLength - overflowSuffix.Length) + overflowSuffix;
        }

        // ------------------------------------------------------------------ Trailing-number parsing

        /// <summary>
        /// Reads the run of decimal digits at the very end of the string. Returns <c>null</c> when the
        /// string does not end in a digit.
        /// </summary>
        public static int? GetTrailingNumber(this string text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            var index = text.Length;
            while (index > 0 && text[index - 1] >= '0' && text[index - 1] <= '9') index--;
            if (index == text.Length) return null;

            return int.Parse(text.Substring(index), CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Reads a trailing "(N)" number, e.g. "Item (3)" yields 3. Returns <c>null</c> when the
        /// pattern is absent.
        /// </summary>
        public static int? GetTrailingParenthesizedNumber(this string text)
        {
            if (string.IsNullOrEmpty(text) || text[text.Length - 1] != ')') return null;

            var open = text.LastIndexOf('(');
            if (open < 0 || open >= text.Length - 2) return null;

            var inner = text.Substring(open + 1, text.Length - open - 2);
            if (!inner.IsNumeric()) return null;

            return int.Parse(inner, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Removes a trailing "(N)" segment (and any whitespace preceding it), e.g. "Item (3)" becomes
        /// "Item". Returns the original string when no such segment exists.
        /// </summary>
        public static string StripTrailingNumberedSuffix(this string text, bool trimTrailingWhitespace = true)
        {
            if (GetTrailingParenthesizedNumber(text) == null) return text;

            var open = text.LastIndexOf('(');
            var result = text.Substring(0, open);
            return trimTrailingWhitespace ? result.TrimEnd() : result;
        }

        // ------------------------------------------------------------------ Encoding / formatting

        private const string HexLower = "0123456789abcdef";

        /// <summary>Renders bytes as a lowercase hexadecimal string with no separators.</summary>
        public static string ToHexString(this byte[] bytes)
        {
            if (bytes == null) return null;
            if (bytes.Length == 0) return string.Empty;

            var chars = new char[bytes.Length * 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                var b = bytes[i];
                chars[i * 2] = HexLower[b >> 4];
                chars[i * 2 + 1] = HexLower[b & 0x0F];
            }
            return new string(chars);
        }

        /// <summary>Renders bytes as a big-endian binary string, 8 bits per byte, no separators.</summary>
        public static string ToBinaryString(this byte[] bytes)
        {
            if (bytes == null) return null;
            if (bytes.Length == 0) return string.Empty;

            var chars = new char[bytes.Length * 8];
            for (var i = 0; i < bytes.Length; i++)
            {
                var b = bytes[i];
                for (var bit = 0; bit < 8; bit++)
                {
                    chars[i * 8 + bit] = (b & (0x80 >> bit)) != 0 ? '1' : '0';
                }
            }
            return new string(chars);
        }

        /// <summary>Normalizes all CR, LF and CRLF line endings to CRLF ("\r\n").</summary>
        public static string NormalizeToCRLF(this string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var builder = new StringBuilder(text.Length + 8);
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '\r')
                {
                    builder.Append("\r\n");
                    if (i + 1 < text.Length && text[i + 1] == '\n') i++; // consume the paired LF
                }
                else if (c == '\n')
                {
                    builder.Append("\r\n");
                }
                else
                {
                    builder.Append(c);
                }
            }
            return builder.ToString();
        }

        /// <summary>
        /// Formats a count together with a noun, appending "s" when the count is not exactly one, e.g.
        /// (1, "apple") -> "1 apple", (3, "apple") -> "3 apples".
        /// </summary>
        public static string ToStringWithEnglishPluralPostfix(this int count, string singularNoun,
            string pluralPostfix = "s")
        {
            var noun = count == 1 ? singularNoun : singularNoun + pluralPostfix;
            return count.ToString(CultureInfo.InvariantCulture) + " " + noun;
        }

        // ------------------------------------------------------------------ Base64

        /// <summary>UTF-8 encodes the text and returns its Base64 representation.</summary>
        public static string ToBase64(this string text)
        {
            if (text == null) return null;
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        }

        /// <summary>Decodes a Base64 string produced by <see cref="ToBase64"/> back to UTF-8 text.</summary>
        public static string FromBase64(this string base64)
        {
            if (base64 == null) return null;
            return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
        }

        // ------------------------------------------------------------------ Wildcards

        /// <summary>
        /// Translates a glob-style wildcard ('*' matches any run, '?' matches one character) into an
        /// anchored regular expression pattern. All other characters are escaped literally.
        /// </summary>
        public static string WildcardToRegex(this string wildcard)
        {
            if (wildcard == null) throw new ArgumentNullException(nameof(wildcard));

            var builder = new StringBuilder(wildcard.Length + 8);
            builder.Append('^');
            foreach (var c in wildcard)
            {
                switch (c)
                {
                    case '*': builder.Append(".*"); break;
                    case '?': builder.Append('.'); break;
                    default: builder.Append(Regex.Escape(c.ToString())); break;
                }
            }
            builder.Append('$');
            return builder.ToString();
        }

        /// <summary>
        /// Tests whether <paramref name="text"/> fully matches the given wildcard pattern. Matching is
        /// case-insensitive by default.
        /// </summary>
        public static bool MatchesWildcard(this string text, string wildcard, bool caseSensitive = false)
        {
            if (text == null || wildcard == null) return false;

            var options = RegexOptions.CultureInvariant | (caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
            return Regex.IsMatch(text, WildcardToRegex(wildcard), options);
        }
    }
}
