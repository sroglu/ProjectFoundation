using System.Collections.Generic;
using System.Text;

namespace PFound.LocalizationService
{
    /// <summary>
    /// Ordered set of custom <see cref="IValueFormatter"/>s tried before the built-ins. Formatters are
    /// consulted in registration order and the first one that succeeds wins; if none do, the built-in
    /// formatters run, and finally the value renders through its own fallback appender. Registration is
    /// expected once at startup.
    /// </summary>
    public sealed class FormatterRegistry
    {
        private readonly List<IValueFormatter> _custom = new List<IValueFormatter>();

        public void Register(IValueFormatter formatter)
        {
            if (formatter == null) return;
            _custom.Add(formatter);
        }

        public void Clear() => _custom.Clear();

        /// <summary>
        /// Renders <paramref name="value"/> under <paramref name="format"/> into <paramref name="output"/>.
        /// Custom formatters first (insertion order, first success wins), then the built-ins, then the
        /// value's own <see cref="ILocalizationValue.AppendTo"/> fallback.
        /// </summary>
        public void Format(ILocalizationValue value, LocalizationFormat format, ILocalizationContext context, StringBuilder output)
        {
            for (int i = 0; i < _custom.Count; i++)
            {
                var scratch = new StringBuilder();
                if (_custom[i].TryFormat(value, format, context, scratch))
                {
                    output.Append(scratch);
                    return;
                }
            }

            var builtIn = new StringBuilder();
            if (BuiltInFormatters.TryFormat(value, format, context, builtIn))
            {
                output.Append(builtIn);
                return;
            }

            value.AppendTo(output);
        }
    }
}
