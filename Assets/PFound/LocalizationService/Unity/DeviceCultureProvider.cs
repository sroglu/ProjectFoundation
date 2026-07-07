using System.Globalization;

namespace PFound.LocalizationService.Unity
{
    /// <summary>
    /// Reads the device's current culture. Isolated here so the engine-free core stays free of any
    /// ambient-culture assumptions and can be unit-tested with explicit inputs.
    /// </summary>
    public static class DeviceCultureProvider
    {
        /// <summary>The device culture code, e.g. "en-US" (empty for the invariant culture).</summary>
        public static string CurrentCultureCode => CultureInfo.CurrentCulture.Name;
    }
}
