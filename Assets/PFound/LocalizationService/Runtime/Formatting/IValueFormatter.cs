using System.Text;

namespace PFound.LocalizationService
{
    /// <summary>
    /// Renders a value under a requested <see cref="LocalizationFormat"/>. A formatter returns false to
    /// decline (the registry then tries the next one, ultimately the built-ins, then a raw fallback).
    /// </summary>
    public interface IValueFormatter
    {
        bool TryFormat(ILocalizationValue value, LocalizationFormat format, ILocalizationContext context, StringBuilder output);
    }
}
