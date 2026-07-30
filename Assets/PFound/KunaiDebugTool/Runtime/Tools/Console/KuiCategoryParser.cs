namespace Kunai
{
    /// <summary>
    /// Parses an optional <c>[Foo]</c> prefix off a Unity log condition string.
    /// Grammar (per research.md R4):
    /// <list type="bullet">
    ///   <item><c>condition[0] == '['</c></item>
    ///   <item>matching <c>']'</c> at index <c>i</c> with <c>1 ≤ i ≤ 32</c> (so
    ///     category name is 1..31 chars)</item>
    ///   <item>characters between contain no nested <c>'['</c></item>
    /// </list>
    /// On match, <c>category = condition[1..i)</c> and the displayed message
    /// is <c>condition[i+1..]</c> with one optional leading space stripped.
    /// On miss, returns the original condition unchanged with <c>null</c>
    /// category. Allocation: zero on miss; one substring + one
    /// <c>Category</c> string on hit (Unity caches both via interning when
    /// the same prefix repeats).
    /// </summary>
    internal static class KuiCategoryParser
    {
        const int MaxCategoryLength = 31;

        public static void Parse(string condition, out string category, out string message)
        {
            category = null;
            message  = condition;
            if (string.IsNullOrEmpty(condition))      { message = string.Empty; return; }
            if (condition[0] != '[')                  return;

            int end = -1;
            int len = condition.Length;
            // i = position of the candidate ']'. Walk until ']' or first
            // disqualifier (nested '[', too long, end of string).
            for (int i = 1; i < len && i <= MaxCategoryLength + 1; i++)
            {
                char c = condition[i];
                if (c == '[') return;            // nested → no match
                if (c == ']') { end = i; break; }
            }
            if (end <= 1) return;                // empty [] or no ']' in range

            int catLen = end - 1;
            if (catLen < 1 || catLen > MaxCategoryLength) return;

            category = condition.Substring(1, catLen);

            int msgStart = end + 1;
            // Strip a single leading space so "[Foo] hello" and "[Foo]hello"
            // both display as "hello".
            if (msgStart < len && condition[msgStart] == ' ') msgStart++;

            message = msgStart >= len ? string.Empty : condition.Substring(msgStart);
        }
    }
}
