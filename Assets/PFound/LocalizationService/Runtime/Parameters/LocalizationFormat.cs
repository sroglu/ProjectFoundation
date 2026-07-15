namespace PFound.LocalizationService
{
    /// <summary>
    /// Closed set of format intents a parameter can carry. Grouped by family: absence, currency, time,
    /// number abbreviation, and location. A formatter registry maps each value to a rendered string; a
    /// parameter that resolves to a localization key bypasses formatting entirely. Timestamp inputs are
    /// interpreted as Unix seconds.
    /// </summary>
    public enum LocalizationFormat
    {
        /// <summary>No formatting requested; the value is rendered by its own fallback appender.</summary>
        None = 0,
        /// <summary>Explicitly declared but with no supported renderer (renders raw).</summary>
        Unsupported,

        // --- currency family ---
        CurrencyWhole,    // "N0"
        CurrencyDecimal,  // "N2"
        CurrencyHourly,   // sign + value + "/" + localized hour abbreviation

        // --- time family (inputs are Unix seconds) ---
        DateShort,        // e.g. M/d/yyyy
        DateLong,         // full weekday + month + time
        DateTimeShort,    // short date + short time
        Duration,         // TimeSpan: "[d ]hh:mm:ss"

        // --- number family ---
        NumberAbbreviated, // K / M / B / T / q / Q with 2 significant digits

        // --- location family ---
        Coordinates,      // "X:<x> Y:<z>"
        Size,             // "<n>x<m>"
        BracketedSize     // "[<n>x<m>]"
    }
}
