using System.Globalization;

namespace PFound.LocalizationService
{
    /// <summary>
    /// Ambient information a formatter needs: the culture to format under and a hook back into the
    /// resolver so a formatter can localize a helper key (for example the abbreviation of "hour" used by
    /// the hourly-currency formatter).
    /// </summary>
    public interface ILocalizationContext
    {
        CultureInfo Culture { get; }

        /// <summary>Localized text for <paramref name="key"/>, or the raw key string if unresolved.</summary>
        string Localize(LocalizationKey key);
    }
}
