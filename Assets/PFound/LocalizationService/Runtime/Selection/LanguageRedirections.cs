using System;
using System.Collections.Generic;

namespace PFound.LocalizationService
{
    /// <summary>
    /// Maps a requested language code to a substitute when the request is not directly supported. Holds
    /// the built-in rules (<c>en-UK</c> → <c>en-GB</c>, and any <c>en-</c>* → <c>en-US</c>) plus any
    /// data-driven rules loaded from the definitions file. Exact-code rules win over prefix rules.
    /// </summary>
    public sealed class LanguageRedirections
    {
        private readonly Dictionary<string, string> _exact =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<KeyValuePair<string, string>> _prefix =
            new List<KeyValuePair<string, string>>();

        public static LanguageRedirections CreateDefault()
        {
            var r = new LanguageRedirections();
            r.AddExact("en-UK", "en-GB");
            r.AddPrefix("en-", LocalizationConstants.DefaultLanguageCode);
            return r;
        }

        public void AddExact(string from, string to) => _exact[from] = to;

        public void AddPrefix(string fromPrefix, string to)
            => _prefix.Add(new KeyValuePair<string, string>(fromPrefix, to));

        /// <summary>
        /// A rule <c>from=to</c>: if <paramref name="from"/> ends with the field delimiter it is a
        /// prefix rule, otherwise an exact-code rule.
        /// </summary>
        public void Add(string from, string to)
        {
            if (!string.IsNullOrEmpty(from) && from[from.Length - 1] == LocalizationConstants.ParameterFieldDelimiter)
                AddPrefix(from, to);
            else
                AddExact(from, to);
        }

        public bool TryRedirect(string code, out string redirected)
        {
            if (!string.IsNullOrEmpty(code))
            {
                if (_exact.TryGetValue(code, out redirected)) return true;
                for (int i = 0; i < _prefix.Count; i++)
                    if (code.StartsWith(_prefix[i].Key, StringComparison.OrdinalIgnoreCase))
                    { redirected = _prefix[i].Value; return true; }
            }
            redirected = null;
            return false;
        }
    }
}
