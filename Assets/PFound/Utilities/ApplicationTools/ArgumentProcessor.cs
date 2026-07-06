using System;
using System.Collections.Generic;

namespace PFound.Utilities.ApplicationTools
{
    /// <summary>
    /// Parses a command-line argument vector into named options and bare
    /// positional tokens. Supported option forms:
    /// <list type="bullet">
    ///   <item><c>--name value</c> / <c>-name value</c></item>
    ///   <item><c>--name=value</c> / <c>-name=value</c></item>
    ///   <item><c>--flag</c> (present with no value)</item>
    /// </list>
    /// Option names are matched case-insensitively with any leading dashes
    /// stripped.
    /// </summary>
    public sealed class ArgumentProcessor
    {
        private readonly Dictionary<string, string> _values =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _flags =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _positional = new List<string>();

        public ArgumentProcessor(string[] arguments)
        {
            if (arguments == null)
                throw new ArgumentNullException(nameof(arguments));

            Parse(arguments);
        }

        /// <summary>Tokens that were not options and not consumed as option values.</summary>
        public IReadOnlyList<string> PositionalArguments => _positional;

        private void Parse(string[] arguments)
        {
            for (int i = 0; i < arguments.Length; i++)
            {
                string token = arguments[i];
                if (string.IsNullOrEmpty(token))
                    continue;

                if (!IsOption(token))
                {
                    _positional.Add(token);
                    continue;
                }

                string name = StripLeadingDashes(token);

                int equals = name.IndexOf('=');
                if (equals >= 0)
                {
                    string key = name.Substring(0, equals);
                    string val = name.Substring(equals + 1);
                    _values[key] = val;
                    continue;
                }

                // Look ahead: if the next token is a value (not another option),
                // treat this as name/value; otherwise record it as a bare flag.
                if (i + 1 < arguments.Length && !IsOption(arguments[i + 1]))
                {
                    _values[name] = arguments[i + 1];
                    i++;
                }
                else
                {
                    _flags.Add(name);
                }
            }
        }

        /// <summary>True when the named flag was present (with or without a value).</summary>
        public bool HasOption(string name)
        {
            string key = StripLeadingDashes(name);
            return _flags.Contains(key) || _values.ContainsKey(key);
        }

        /// <summary>
        /// Returns the value associated with a named option, or
        /// <paramref name="defaultValue"/> when the option has no value.
        /// </summary>
        public string GetValue(string name, string defaultValue = null)
        {
            return _values.TryGetValue(StripLeadingDashes(name), out string value)
                ? value
                : defaultValue;
        }

        /// <summary>Attempts to read the value of a named option.</summary>
        public bool TryGetValue(string name, out string value)
        {
            return _values.TryGetValue(StripLeadingDashes(name), out value);
        }

        private static bool IsOption(string token)
        {
            return token.Length > 1 && token[0] == '-';
        }

        private static string StripLeadingDashes(string token)
        {
            int start = 0;
            while (start < token.Length && token[start] == '-')
                start++;
            return token.Substring(start);
        }
    }
}
