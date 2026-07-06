using System;
using Cysharp.Text;

namespace PFound.Utilities.StringTools
{
    // ZString-facing half of the tag processor. Reuses the engine-free TagScanner (see
    // TagProcessing.cs) but streams each piece straight into a Utf16ValueStringBuilder, avoiding
    // the intermediate string allocations of the ITagSink overload. This file is the only part of
    // StringTools that depends on Cysharp.Text, so it is excluded from the mono/csc test build.
    public static partial class StringTools
    {
        /// <summary>
        /// ZString-facing tag consumer. Each callback appends into the caller's growing
        /// <see cref="Utf16ValueStringBuilder"/>, which is passed by ref because it is a ref struct
        /// that must not be copied.
        /// </summary>
        public interface ITagProcessor
        {
            /// <summary>Append a run of literal text (outside any braces) to the builder.</summary>
            void AppendText(ref Utf16ValueStringBuilder stringBuilder, ReadOnlySpan<char> partOfText);

            /// <summary>Resolve one tag body (braces stripped) and append its expansion.</summary>
            void AppendTag(ref Utf16ValueStringBuilder stringBuilder, ReadOnlySpan<char> tag);
        }

        /// <summary>
        /// ZString overload of <see cref="ProcessTags(string,char,char,ITagSink)"/>: runs the same
        /// brace scanner but forwards each literal run and tag body to <paramref name="processor"/>
        /// together with the <paramref name="output"/> builder. Returns the same
        /// <see cref="TagProcessResult"/> outcomes; on failure the caller is expected to discard the
        /// partially built output.
        /// </summary>
        public static TagProcessResult ProcessTags(this string input, char openBrace, char closeBrace,
            ITagProcessor processor, ref Utf16ValueStringBuilder output)
        {
            if (processor == null) throw new ArgumentNullException(nameof(processor));

            var scanner = new TagScanner(input.AsSpan(), openBrace, closeBrace);
            while (scanner.MoveNext())
            {
                if (scanner.CurrentIsTag) processor.AppendTag(ref output, scanner.Current);
                else processor.AppendText(ref output, scanner.Current);
            }
            return scanner.Result;
        }
    }
}
