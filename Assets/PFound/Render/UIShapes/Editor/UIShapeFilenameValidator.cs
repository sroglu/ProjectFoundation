using System.Text;

namespace PFound.Render.UIShapes.Editor
{
    /// <summary>
    /// Validates + sanitizes user-typed filenames for the bake EditorWindow's save flow.
    /// Filesystem-safe rule set: <c>[A-Za-z0-9_-]</c> characters allowed; everything else
    /// is replaced with <c>'_'</c>. Empty input, whitespace-only input, or input that begins
    /// with a literal <c>'.'</c> is rejected outright.
    /// </summary>
    public static class UIShapeFilenameValidator
    {
        private const char Replacement = '_';

        /// <summary>
        /// Validates <paramref name="name"/> for use as a bake filename.
        /// </summary>
        /// <param name="name">User input (without extension).</param>
        /// <param name="sanitized">When the return value is <c>true</c>, holds the safe filename
        /// (which may differ from <paramref name="name"/> when offending characters were replaced).
        /// When the return value is <c>false</c>, holds an empty string.</param>
        /// <param name="warning">When sanitization changed the name, holds a one-line description
        /// (suitable for an Inspector HelpBox / modal). Empty when no change was needed.</param>
        /// <returns><c>true</c> when the input is non-empty / non-leading-<c>.</c> and the sanitized
        /// result is non-empty; <c>false</c> otherwise.</returns>
        public static bool TryValidate(string name, out string sanitized, out string warning)
        {
            sanitized = string.Empty;
            warning = string.Empty;

            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            // Whitespace-only / leading-dot rejected.
            bool allWhitespace = true;
            for (int i = 0; i < name.Length; i++)
            {
                if (!char.IsWhiteSpace(name[i])) { allWhitespace = false; break; }
            }
            if (allWhitespace) return false;
            if (name[0] == '.') return false;

            var sb = new StringBuilder(name.Length);
            bool changed = false;
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool ok = (c >= 'A' && c <= 'Z')
                       || (c >= 'a' && c <= 'z')
                       || (c >= '0' && c <= '9')
                       || c == '_'
                       || c == '-';
                if (ok)
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append(Replacement);
                    changed = true;
                }
            }

            sanitized = sb.ToString();
            if (sanitized.Length == 0)
            {
                sanitized = string.Empty;
                return false;
            }

            if (changed)
            {
                warning = "Filename contained characters outside [A-Za-z0-9_-]; replaced with '" + Replacement + "'.";
            }
            return true;
        }
    }
}
