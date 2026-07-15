using System;

namespace PFound.Utilities.StringTools
{
    // Engine-free half of the tag processor. Splits a string into literal runs and
    // brace-delimited {tag} sections, streaming each piece to a sink. This file carries the
    // single-pass scanner (TagScanner) shared with the ZString overload, so it compiles and
    // tests under plain mono/csc with no third-party string builder.
    public static partial class StringTools
    {
        /// <summary>
        /// Outcome of a tag-processing pass. The three <c>Succeeded*</c> members mean the scan
        /// completed and the emitted output is valid; the three <c>Failed*</c> members mean a
        /// brace-structure error was hit and the scan stopped at the offending brace.
        /// </summary>
        public enum TagProcessResult
        {
            /// <summary>Scan finished and at least one <c>{tag}</c> was emitted.</summary>
            Succeeded,

            /// <summary>Scan finished but the input held no tags — it was pure literal text.</summary>
            SucceededButFoundNoTags,

            /// <summary>Input was null or empty, so there was nothing to scan.</summary>
            SucceededButEmptyInputText,

            /// <summary>An opening brace appeared while a tag was still open, e.g. "{a{b}}".</summary>
            FailedDueToNestedTagBraces,

            /// <summary>An opening brace was never closed, or a closing brace had no opener.</summary>
            FailedDueToMismatchingTagBraces,

            /// <summary>A brace pair enclosed no characters, e.g. "{}".</summary>
            FailedDueToEmptyTagBraces,
        }

        /// <summary>
        /// Receives the ordered pieces a tag scan produces: literal text runs and the inner text of
        /// each brace-delimited tag. Engine-free counterpart of <see cref="ITagProcessor"/>.
        /// </summary>
        public interface ITagSink
        {
            /// <summary>A run of literal text that lies outside any braces.</summary>
            void AppendText(ReadOnlySpan<char> text);

            /// <summary>The inner text of one <c>{tag}</c>, with the braces stripped.</summary>
            void AppendTag(ReadOnlySpan<char> tag);
        }

        /// <summary>
        /// Walks <paramref name="input"/>, splitting it into literal runs and <c>{tag}</c> sections
        /// delimited by <paramref name="openBrace"/> / <paramref name="closeBrace"/>, and forwards
        /// each piece to <paramref name="sink"/> in order. Returns a <see cref="TagProcessResult"/>
        /// describing the outcome; on a brace-structure failure the scan stops and the sink keeps
        /// only what was emitted before the offending brace.
        /// </summary>
        public static TagProcessResult ProcessTags(this string input, char openBrace, char closeBrace,
            ITagSink sink)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));

            var scanner = new TagScanner(input.AsSpan(), openBrace, closeBrace);
            while (scanner.MoveNext())
            {
                if (scanner.CurrentIsTag) sink.AppendTag(scanner.Current);
                else sink.AppendText(scanner.Current);
            }
            return scanner.Result;
        }

        /// <summary>
        /// Single-pass brace scanner shared by every <c>ProcessTags</c> overload. Implemented as a
        /// ref struct so it can hand back <see cref="ReadOnlySpan{T}"/> slices of the source with no
        /// allocation. Call <see cref="MoveNext"/> until it returns <c>false</c>; the final
        /// <see cref="Result"/> then describes how the walk ended.
        /// </summary>
        internal ref struct TagScanner
        {
            private readonly ReadOnlySpan<char> _text;
            private readonly char _open;
            private readonly char _close;
            private int _cursor;
            private bool _emittedTag;
            private bool _stopped;

            /// <summary>The piece produced by the most recent successful <see cref="MoveNext"/>.</summary>
            public ReadOnlySpan<char> Current;

            /// <summary>True when <see cref="Current"/> is a tag body; false when it is literal text.</summary>
            public bool CurrentIsTag;

            /// <summary>The running outcome; final once <see cref="MoveNext"/> returns <c>false</c>.</summary>
            public TagProcessResult Result;

            public TagScanner(ReadOnlySpan<char> text, char openBrace, char closeBrace)
            {
                _text = text;
                _open = openBrace;
                _close = closeBrace;
                _cursor = 0;
                _emittedTag = false;
                _stopped = text.Length == 0;
                Current = default;
                CurrentIsTag = false;
                Result = text.Length == 0
                    ? TagProcessResult.SucceededButEmptyInputText
                    : TagProcessResult.SucceededButFoundNoTags;
            }

            public bool MoveNext()
            {
                if (_stopped) return false;

                if (_cursor >= _text.Length)
                {
                    _stopped = true;
                    Result = _emittedTag
                        ? TagProcessResult.Succeeded
                        : TagProcessResult.SucceededButFoundNoTags;
                    return false;
                }

                var c = _text[_cursor];

                // A closing brace here has no matching opener.
                if (c == _close) return Fail(TagProcessResult.FailedDueToMismatchingTagBraces);

                if (c == _open)
                {
                    var bodyStart = _cursor + 1;
                    var probe = bodyStart;
                    while (probe < _text.Length)
                    {
                        var inner = _text[probe];
                        if (inner == _open) return Fail(TagProcessResult.FailedDueToNestedTagBraces);
                        if (inner == _close) break;
                        probe++;
                    }

                    if (probe >= _text.Length) return Fail(TagProcessResult.FailedDueToMismatchingTagBraces);
                    if (probe == bodyStart) return Fail(TagProcessResult.FailedDueToEmptyTagBraces);

                    Current = _text.Slice(bodyStart, probe - bodyStart);
                    CurrentIsTag = true;
                    _emittedTag = true;
                    _cursor = probe + 1;
                    return true;
                }

                // Literal run: everything up to the next brace of either kind.
                var runStart = _cursor;
                var runEnd = _cursor;
                while (runEnd < _text.Length && _text[runEnd] != _open && _text[runEnd] != _close) runEnd++;

                Current = _text.Slice(runStart, runEnd - runStart);
                CurrentIsTag = false;
                _cursor = runEnd;
                return true;
            }

            private bool Fail(TagProcessResult failure)
            {
                Result = failure;
                _stopped = true;
                return false;
            }
        }
    }
}
