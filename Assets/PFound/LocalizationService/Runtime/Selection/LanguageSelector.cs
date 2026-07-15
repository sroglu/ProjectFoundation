using System.Collections.Generic;

namespace PFound.LocalizationService
{
    /// <summary>
    /// Pure decision logic for which language to activate at startup, engine-free: it takes the
    /// persisted user preference and the device culture code as plain strings and consults the supported
    /// set + redirections. Priority: a supported preference wins; else a redirected preference; else the
    /// device culture (direct or redirected); else the default language. The Unity adapter supplies the
    /// preference and device-culture strings and persists the outcome.
    /// </summary>
    public static class LanguageSelector
    {
        /// <summary>
        /// Resolves the active language.
        /// </summary>
        /// <param name="preferred">Persisted preference code, or null/empty if none.</param>
        /// <param name="deviceCulture">Device culture code (e.g. CultureInfo.CurrentCulture.Name).</param>
        /// <param name="supported">Supported languages.</param>
        /// <param name="redirections">Redirection rules (default rules if null).</param>
        /// <param name="clearPreference">
        /// True when the caller should drop the stored preference because it was unsupported and could
        /// not be redirected — selection then continued from the device culture.
        /// </param>
        public static LanguageKey Resolve(
            string preferred,
            string deviceCulture,
            IReadOnlyList<LanguageKey> supported,
            LanguageRedirections redirections,
            out bool clearPreference)
        {
            redirections = redirections ?? LanguageRedirections.CreateDefault();
            clearPreference = false;

            if (!string.IsNullOrEmpty(preferred))
            {
                if (TryResolveCode(preferred, supported, redirections, out var fromPref))
                    return fromPref;
                // Preference is stale: it should be cleared, then fall through to the device culture.
                clearPreference = true;
            }

            if (TryResolveCode(deviceCulture, supported, redirections, out var fromDevice))
                return fromDevice;

            return new LanguageKey(LocalizationConstants.DefaultLanguageCode);
        }

        /// <summary>Resolves a single code to a supported language directly or via redirection.</summary>
        public static bool TryResolveCode(
            string code,
            IReadOnlyList<LanguageKey> supported,
            LanguageRedirections redirections,
            out LanguageKey resolved)
        {
            resolved = default;
            if (string.IsNullOrEmpty(code)) return false;

            var candidate = new LanguageKey(code);
            if (IsSupported(candidate, supported)) { resolved = candidate; return true; }

            redirections = redirections ?? LanguageRedirections.CreateDefault();
            if (redirections.TryRedirect(code, out var target))
            {
                var redirected = new LanguageKey(target);
                if (IsSupported(redirected, supported)) { resolved = redirected; return true; }
            }
            return false;
        }

        private static bool IsSupported(LanguageKey key, IReadOnlyList<LanguageKey> supported)
        {
            if (supported == null) return false;
            for (int i = 0; i < supported.Count; i++)
                if (supported[i].Equals(key)) return true;
            return false;
        }
    }
}
