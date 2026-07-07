using System;
using System.Globalization;

namespace PFound.LocalizationService
{
    /// <summary>
    /// Compact number rendering with magnitude suffixes. Bands span three orders of magnitude each:
    /// &lt;1e3 none, &lt;1e6 K, &lt;1e9 M, &lt;1e12 B, &lt;1e15 T, &lt;1e18 q, ≥1e18 Q. The mantissa keeps
    /// two significant digits: when the second significant digit is zero it is dropped (no decimal
    /// point), otherwise one decimal place is shown (e.g. 1,234,567 → "1.2M").
    /// </summary>
    public static class NumberAbbreviator
    {
        private static readonly double[] Thresholds = { 1e3, 1e6, 1e9, 1e12, 1e15, 1e18 };
        private static readonly string[] Suffixes = { "K", "M", "B", "T", "q", "Q" };

        public static string Abbreviate(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return value.ToString(CultureInfo.InvariantCulture);

            string sign = value < 0 ? "-" : string.Empty;
            double abs = Math.Abs(value);

            if (abs < Thresholds[0])
            {
                // No abbreviation below one thousand: render as a whole number.
                return sign + ((long)abs).ToString(CultureInfo.InvariantCulture);
            }

            // Pick the band: the largest threshold not exceeding abs.
            int band = 0;
            for (int i = 0; i < Thresholds.Length; i++)
                if (abs >= Thresholds[i]) band = i;

            string suffix = Suffixes[band];
            double scaled = abs / Thresholds[band]; // in [1, 1000)

            string mantissa;
            if (scaled >= 100.0)
            {
                // Two significant digits => round to the nearest ten, no decimal.
                long rounded = (long)Math.Round(scaled / 10.0, MidpointRounding.AwayFromZero) * 10;
                mantissa = rounded.ToString(CultureInfo.InvariantCulture);
            }
            else if (scaled >= 10.0)
            {
                // Two significant digits are both integer digits: no decimal.
                long rounded = (long)Math.Round(scaled, MidpointRounding.AwayFromZero);
                mantissa = rounded.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                // One integer digit: the second significant digit is the first decimal.
                double rounded = Math.Round(scaled, 1, MidpointRounding.AwayFromZero);
                mantissa = (rounded % 1.0 == 0.0)
                    ? ((long)rounded).ToString(CultureInfo.InvariantCulture)
                    : rounded.ToString("0.0", CultureInfo.InvariantCulture);
            }

            return sign + mantissa + suffix;
        }
    }
}
