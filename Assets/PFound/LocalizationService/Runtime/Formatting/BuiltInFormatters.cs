using System;
using System.Globalization;
using System.Text;

namespace PFound.LocalizationService
{
    /// <summary>
    /// Default rendering for every <see cref="LocalizationFormat"/>: currency (whole/decimal/hourly),
    /// time (short/long date, short datetime, duration), abbreviated numbers, and location (coordinates,
    /// size, bracketed size). Timestamp inputs are Unix seconds. Consulted after the custom registry.
    /// </summary>
    public static class BuiltInFormatters
    {
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static bool TryFormat(ILocalizationValue value, LocalizationFormat format, ILocalizationContext context, StringBuilder output)
        {
            var culture = context != null ? context.Culture : CultureInfo.CurrentCulture;

            switch (format)
            {
                case LocalizationFormat.CurrencyWhole:
                    return Number(value, "N0", culture, output);
                case LocalizationFormat.CurrencyDecimal:
                    return Number(value, "N2", culture, output);
                case LocalizationFormat.CurrencyHourly:
                    return Hourly(value, context, culture, output);

                case LocalizationFormat.DateShort:
                    return Time(value, culture, output, DateStyle.ShortDate);
                case LocalizationFormat.DateLong:
                    return Time(value, culture, output, DateStyle.LongDate);
                case LocalizationFormat.DateTimeShort:
                    return Time(value, culture, output, DateStyle.ShortDateTime);
                case LocalizationFormat.Duration:
                    return Duration(value, output);

                case LocalizationFormat.NumberAbbreviated:
                    if (!value.TryGetDouble(out double n)) return false;
                    output.Append(NumberAbbreviator.Abbreviate(n));
                    return true;

                case LocalizationFormat.Coordinates:
                    if (!value.TryGetCoordinates(out double x, out double z)) return false;
                    output.Append("X:").Append(x.ToString(culture)).Append(" Y:").Append(z.ToString(culture));
                    return true;
                case LocalizationFormat.Size:
                    if (!value.TryGetCoordinates(out double sw, out double sh)) return false;
                    output.Append(Whole(sw, culture)).Append('x').Append(Whole(sh, culture));
                    return true;
                case LocalizationFormat.BracketedSize:
                    if (!value.TryGetCoordinates(out double bw, out double bh)) return false;
                    output.Append('[').Append(Whole(bw, culture)).Append('x').Append(Whole(bh, culture)).Append(']');
                    return true;

                default:
                    return false; // None / Unsupported -> caller falls back to the value's own appender
            }
        }

        private static bool Number(ILocalizationValue value, string fmt, CultureInfo culture, StringBuilder output)
        {
            if (!value.TryGetDouble(out double d)) return false;
            output.Append(d.ToString(fmt, culture));
            return true;
        }

        private static bool Hourly(ILocalizationValue value, ILocalizationContext context, CultureInfo culture, StringBuilder output)
        {
            if (!value.TryGetDouble(out double d)) return false;
            if (d >= 0) output.Append('+');
            output.Append(d.ToString(culture)).Append('/');
            string hour = context != null
                ? context.Localize(new LocalizationKey(LocalizationConstants.HourAbbreviationKey))
                : LocalizationConstants.HourAbbreviationKey;
            output.Append(hour);
            return true;
        }

        private enum DateStyle { ShortDate, LongDate, ShortDateTime }

        private static bool Time(ILocalizationValue value, CultureInfo culture, StringBuilder output, DateStyle style)
        {
            if (!TryGetDateTime(value, out DateTime dt)) return false;
            var local = dt.ToLocalTime();
            switch (style)
            {
                case DateStyle.ShortDate:
                    output.Append(local.ToString("d", culture)); break;
                case DateStyle.LongDate:
                    output.Append(local.ToString("f", culture)); break;
                case DateStyle.ShortDateTime:
                    output.Append(local.ToString("g", culture)); break;
            }
            return true;
        }

        private static bool Duration(ILocalizationValue value, StringBuilder output)
        {
            TimeSpan span;
            if (value.TryGetTimeSpan(out span)) { }
            else if (value.TryGetDouble(out double seconds)) span = TimeSpan.FromSeconds(seconds);
            else return false;

            if (span.Days > 0) output.Append(span.Days).Append(' ');
            output.Append(((int)span.Hours).ToString("00")).Append(':')
                  .Append(((int)span.Minutes).ToString("00")).Append(':')
                  .Append(((int)span.Seconds).ToString("00"));
            return true;
        }

        private static bool TryGetDateTime(ILocalizationValue value, out DateTime dt)
        {
            if (value.TryGetDouble(out double unixSeconds))
            {
                dt = Epoch.AddSeconds(unixSeconds);
                return true;
            }
            dt = default;
            return false;
        }

        private static string Whole(double v, CultureInfo culture)
            => ((long)Math.Round(v, MidpointRounding.AwayFromZero)).ToString(culture);
    }
}
