using System;
using System.Globalization;
using System.Text;

namespace PFound.LocalizationService
{
    /// <summary>Wraps a plain number as a localization value.</summary>
    public sealed class NumberValue : ILocalizationValue
    {
        private readonly double _value;
        public NumberValue(double value) { _value = value; }
        public bool TryGetDouble(out double value) { value = _value; return true; }
        public bool TryGetTimeSpan(out TimeSpan value) { value = default; return false; }
        public bool TryGetCoordinates(out double x, out double z) { x = z = 0; return false; }
        public bool TryGetLocalizationKey(out LocalizationKey key) { key = default; return false; }
        public void AppendTo(StringBuilder builder) => builder.Append(_value.ToString(CultureInfo.CurrentCulture));
    }

    /// <summary>Wraps a Unix-seconds timestamp (used by the time formatters).</summary>
    public sealed class TimestampValue : ILocalizationValue
    {
        private readonly double _unixSeconds;
        public TimestampValue(double unixSeconds) { _unixSeconds = unixSeconds; }
        public bool TryGetDouble(out double value) { value = _unixSeconds; return true; }
        public bool TryGetTimeSpan(out TimeSpan value) { value = default; return false; }
        public bool TryGetCoordinates(out double x, out double z) { x = z = 0; return false; }
        public bool TryGetLocalizationKey(out LocalizationKey key) { key = default; return false; }
        public void AppendTo(StringBuilder builder) => builder.Append(_unixSeconds.ToString(CultureInfo.CurrentCulture));
    }

    /// <summary>Wraps a duration.</summary>
    public sealed class DurationValue : ILocalizationValue
    {
        private readonly TimeSpan _value;
        public DurationValue(TimeSpan value) { _value = value; }
        public bool TryGetDouble(out double value) { value = _value.TotalSeconds; return true; }
        public bool TryGetTimeSpan(out TimeSpan value) { value = _value; return true; }
        public bool TryGetCoordinates(out double x, out double z) { x = z = 0; return false; }
        public bool TryGetLocalizationKey(out LocalizationKey key) { key = default; return false; }
        public void AppendTo(StringBuilder builder) => builder.Append(_value.ToString());
    }

    /// <summary>Wraps a map position.</summary>
    public sealed class CoordinatesValue : ILocalizationValue
    {
        private readonly double _x, _z;
        public CoordinatesValue(double x, double z) { _x = x; _z = z; }
        public bool TryGetDouble(out double value) { value = 0; return false; }
        public bool TryGetTimeSpan(out TimeSpan value) { value = default; return false; }
        public bool TryGetCoordinates(out double x, out double z) { x = _x; z = _z; return true; }
        public bool TryGetLocalizationKey(out LocalizationKey key) { key = default; return false; }
        public void AppendTo(StringBuilder builder)
        {
            builder.Append(_x.ToString(CultureInfo.CurrentCulture)).Append(',').Append(_z.ToString(CultureInfo.CurrentCulture));
        }
    }

    /// <summary>Wraps a raw string.</summary>
    public sealed class TextValue : ILocalizationValue
    {
        private readonly string _text;
        public TextValue(string text) { _text = text ?? string.Empty; }
        public bool TryGetDouble(out double value) => double.TryParse(_text, NumberStyles.Any, CultureInfo.CurrentCulture, out value);
        public bool TryGetTimeSpan(out TimeSpan value) { value = default; return false; }
        public bool TryGetCoordinates(out double x, out double z) { x = z = 0; return false; }
        public bool TryGetLocalizationKey(out LocalizationKey key) { key = default; return false; }
        public void AppendTo(StringBuilder builder) => builder.Append(_text);
    }

    /// <summary>A value that renders as the localized text of a nominated key.</summary>
    public sealed class KeyValue : ILocalizationValue
    {
        private readonly LocalizationKey _key;
        public KeyValue(LocalizationKey key) { _key = key; }
        public bool TryGetDouble(out double value) { value = 0; return false; }
        public bool TryGetTimeSpan(out TimeSpan value) { value = default; return false; }
        public bool TryGetCoordinates(out double x, out double z) { x = z = 0; return false; }
        public bool TryGetLocalizationKey(out LocalizationKey key) { key = _key; return true; }
        public void AppendTo(StringBuilder builder) => builder.Append(_key.Value);
    }

    /// <summary>
    /// Wraps an enum value. Renders via its auto localization key (<c>{prefix}_{name}</c>), exposes the
    /// numeric value for number formatting, and appends the value name as its plain-text fallback.
    /// </summary>
    public sealed class EnumValue : ILocalizationValue
    {
        private readonly Enum _value;
        public EnumValue(Enum value) { _value = value ?? throw new ArgumentNullException(nameof(value)); }

        public bool TryGetDouble(out double value) { value = Convert.ToDouble(_value, CultureInfo.InvariantCulture); return true; }
        public bool TryGetTimeSpan(out TimeSpan value) { value = default; return false; }
        public bool TryGetCoordinates(out double x, out double z) { x = z = 0; return false; }
        public bool TryGetLocalizationKey(out LocalizationKey key)
        {
            key = new LocalizationKey(EnumKeyGenerator.KeyFor(_value));
            return true;
        }
        public void AppendTo(StringBuilder builder) => builder.Append(_value.ToString());
    }
}
