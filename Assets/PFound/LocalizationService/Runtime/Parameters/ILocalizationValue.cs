using System;
using System.Text;

namespace PFound.LocalizationService
{
    /// <summary>
    /// A parameter value that can be injected into a localized string. It exposes the shapes a formatter
    /// might need (a number, a duration, a pair of map coordinates), can nominate itself as a
    /// localization key (so it renders as localized text rather than being formatted), and always
    /// provides a plain-text fallback appender used when nothing else applies.
    /// </summary>
    public interface ILocalizationValue
    {
        /// <summary>True + the numeric value if this can be treated as a number.</summary>
        bool TryGetDouble(out double value);

        /// <summary>True + a duration if this represents a span of time.</summary>
        bool TryGetTimeSpan(out TimeSpan value);

        /// <summary>True + a map position if this represents a location.</summary>
        bool TryGetCoordinates(out double x, out double z);

        /// <summary>
        /// True + a localization key if this value should render as localized text. When true, the key
        /// is resolved and its localized text wins over any format-based rendering.
        /// </summary>
        bool TryGetLocalizationKey(out LocalizationKey key);

        /// <summary>Appends the plain-text representation used when no other rendering applies.</summary>
        void AppendTo(StringBuilder builder);
    }
}
