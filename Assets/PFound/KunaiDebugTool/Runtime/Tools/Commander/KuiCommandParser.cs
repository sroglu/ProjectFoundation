using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Kunai
{
    /// <summary>
    /// Stateless argv tokenizer + parameter binder for the Commander REPL.
    /// Tokenizer splits on whitespace except inside double-quoted spans.
    /// Quotes are stripped; <c>\</c> inside quotes escapes the next character.
    /// Binder converts each token to the corresponding method-parameter type
    /// (primitives via <see cref="Convert.ChangeType(object, Type, IFormatProvider)"/>,
    /// enums via <see cref="Enum.TryParse{TEnum}(string, bool, out TEnum)"/>, bool via a
    /// hard-coded path so <c>"0"</c>/<c>"1"</c>/<c>"yes"</c>/<c>"no"</c> work).
    /// </summary>
    internal static class KuiCommandParser
    {
        // Reused by Tokenize per call; method is not thread-safe (Commander runs
        // on the main thread, so that's fine).
        static readonly StringBuilder s_token = new();

        /// <summary>
        /// Tokenize <paramref name="line"/>. On success returns true and fills
        /// <paramref name="tokens"/>. On failure returns false with a one-line
        /// reason in <paramref name="error"/>.
        /// </summary>
        public static bool TryTokenize(string line, List<string> tokens, out string error)
        {
            tokens.Clear();
            error = null;
            if (string.IsNullOrWhiteSpace(line)) return true;

            bool inQuote = false;
            s_token.Clear();
            int len = line.Length;

            for (int i = 0; i < len; i++)
            {
                char c = line[i];
                if (inQuote)
                {
                    if (c == '\\')
                    {
                        if (i + 1 < len) { s_token.Append(line[i + 1]); i++; }
                        else { error = "trailing backslash inside quoted string"; return false; }
                    }
                    else if (c == '"')
                    {
                        inQuote = false;
                    }
                    else
                    {
                        s_token.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuote = true;
                    }
                    else if (char.IsWhiteSpace(c))
                    {
                        if (s_token.Length > 0) { tokens.Add(s_token.ToString()); s_token.Clear(); }
                    }
                    else
                    {
                        s_token.Append(c);
                    }
                }
            }

            if (inQuote) { error = "unbalanced double quote"; return false; }
            if (s_token.Length > 0) { tokens.Add(s_token.ToString()); s_token.Clear(); }
            return true;
        }

        /// <summary>
        /// Bind the argv slice (excluding command name at args[0]) into a
        /// boxed-args array matching <paramref name="cmd"/>.Parameters. Returns
        /// false on arity mismatch or any conversion failure with the reason
        /// in <paramref name="error"/>.
        /// </summary>
        public static bool TryBind(KuiCommandEntry cmd, IList<string> args, int firstArg,
                                   out object[] boxed, out string error)
        {
            boxed = null;
            error = null;
            int provided = args.Count - firstArg;
            int expected = cmd.Parameters.Length;
            if (provided != expected)
            {
                error = $"expected {expected} arg(s), got {provided}";
                return false;
            }

            boxed = new object[expected];
            for (int i = 0; i < expected; i++)
            {
                var p = cmd.Parameters[i];
                string token = args[firstArg + i];
                if (!TryConvert(token, p.ParameterType, out boxed[i], out string convErr))
                {
                    error = $"arg #{i + 1} ({p.Name}): {convErr}";
                    return false;
                }
            }
            return true;
        }

        static bool TryConvert(string token, Type targetType, out object value, out string error)
        {
            value = null;
            error = null;

            if (targetType == typeof(string))
            {
                value = token;
                return true;
            }
            if (targetType == typeof(bool))
            {
                if (TryParseBool(token, out bool b)) { value = b; return true; }
                error = $"cannot parse '{token}' as bool";
                return false;
            }
            if (targetType.IsEnum)
            {
                try
                {
                    value = Enum.Parse(targetType, token, ignoreCase: true);
                    return true;
                }
                catch (Exception ex)
                {
                    error = $"cannot parse '{token}' as {targetType.Name}: {ex.Message}";
                    return false;
                }
            }

            // Primitives via Convert.ChangeType using invariant culture so "1.5"
            // is the float regardless of system locale.
            try
            {
                value = Convert.ChangeType(token, targetType, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception ex)
            {
                error = $"cannot parse '{token}' as {targetType.Name}: {ex.Message}";
                return false;
            }
        }

        static bool TryParseBool(string token, out bool result)
        {
            switch (token)
            {
                case "true":  case "True":  case "TRUE":
                case "1": case "yes": case "Yes": case "YES":
                case "on":  case "On":  case "ON":
                    result = true; return true;
                case "false": case "False": case "FALSE":
                case "0": case "no": case "No": case "NO":
                case "off": case "Off": case "OFF":
                    result = false; return true;
            }
            return bool.TryParse(token, out result);
        }
    }
}
