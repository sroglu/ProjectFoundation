using System.Text;

namespace PFound.LocalizationService
{
    /// <summary>Result of a single tag-scan pass.</summary>
    public enum TagScanOutcome
    {
        /// <summary>Well-formed (possibly with no tags at all, or empty input) — output is valid.</summary>
        Accepted,
        /// <summary>An open bracket appeared while already inside a tag.</summary>
        RejectedNested,
        /// <summary>A close without an open, or an open never closed.</summary>
        RejectedMismatched,
        /// <summary>An empty tag <c>{}</c>.</summary>
        RejectedEmptyTag
    }

    /// <summary>
    /// Handler invoked for each tag body found between the open and close brackets. Implementations
    /// append the tag's rendered text to the supplied builder.
    /// </summary>
    public delegate void TagResolver(string tagName, StringBuilder output);

    /// <summary>
    /// Generic one-pass open/close bracket scanner. Literal text outside brackets is copied through;
    /// text between a matched open/close pair is handed to a <see cref="TagResolver"/>. Brackets do not
    /// nest and must be balanced; empty tags are rejected. On any rejection the original input is
    /// returned unchanged and the reason is reported through the outcome.
    /// </summary>
    public static class TagScanner
    {
        public static TagScanOutcome Process(string input, char open, char close, TagResolver resolver, out string result)
        {
            if (string.IsNullOrEmpty(input)) { result = input ?? string.Empty; return TagScanOutcome.Accepted; }

            var sb = new StringBuilder(input.Length);
            bool inside = false;
            int tagStart = 0;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c == open)
                {
                    if (inside) { result = input; return TagScanOutcome.RejectedNested; }
                    inside = true;
                    tagStart = i + 1;
                }
                else if (c == close)
                {
                    if (!inside) { result = input; return TagScanOutcome.RejectedMismatched; }
                    int len = i - tagStart;
                    if (len == 0) { result = input; return TagScanOutcome.RejectedEmptyTag; }
                    string tagName = input.Substring(tagStart, len);
                    resolver?.Invoke(tagName, sb);
                    inside = false;
                }
                else if (!inside)
                {
                    sb.Append(c);
                }
            }

            if (inside) { result = input; return TagScanOutcome.RejectedMismatched; }
            result = sb.ToString();
            return TagScanOutcome.Accepted;
        }
    }
}
