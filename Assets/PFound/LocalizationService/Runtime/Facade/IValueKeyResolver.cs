namespace PFound.LocalizationService
{
    /// <summary>
    /// Optional startup hook that lets a game map a parameter value onto a localization key. When it
    /// yields a key that exists, that localized text is substituted in preference to any format-based
    /// rendering. Set once on the catalog.
    /// </summary>
    public interface IValueKeyResolver
    {
        bool TryResolveKey(ILocalizationValue value, out LocalizationKey key);
    }
}
