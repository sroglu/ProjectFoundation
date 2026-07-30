using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PFound.LocalizationService
{
    /// <summary>
    /// The rich resolution facade. It layers parameter definitions, dynamic (template) keys, tag
    /// substitution, and value formatting on top of the plain <see cref="LocalizationService"/> resolver
    /// spine (which owns the active/fallback content tables, the cache, and language switching).
    ///
    /// A lookup: resolve the (possibly dynamic) key to its raw text via the spine, then run one
    /// left-to-right <c>{tag}</c> pass in which each tag is filled by the matching parameter value —
    /// first as a localization key if the value maps to one (custom resolver hook or the value itself),
    /// otherwise via the formatter registry, otherwise the value's own text. Several lookup variants
    /// differ only in how a missing key is handled.
    /// </summary>
    public sealed class LocalizationCatalog : ILocalizationContext
    {
        private const int MaxDynamicParameters = 2;

        private readonly LocalizationService _resolver;
        private readonly LocalizationDefinitions _definitions;
        private readonly FormatterRegistry _formatters;
        private IValueKeyResolver _valueKeyResolver;
        private CultureInfo _culture;

        [ThreadStatic] private static StringBuilder t_scratch;

        public LocalizationCatalog(
            ILocalizationSource content,
            LanguageKey fallbackLanguage,
            LocalizationDefinitions definitions = null,
            FormatterRegistry formatters = null,
            CultureInfo culture = null)
        {
            _resolver = new LocalizationService(content, fallbackLanguage);
            _definitions = definitions ?? new LocalizationDefinitions();
            _formatters = formatters ?? new FormatterRegistry();
            _culture = culture ?? CultureInfo.CurrentCulture;
        }

        // --- configuration ---
        public void SetValueKeyResolver(IValueKeyResolver resolver) => _valueKeyResolver = resolver;
        public void SetCulture(CultureInfo culture) => _culture = culture ?? CultureInfo.CurrentCulture;
        public FormatterRegistry Formatters => _formatters;
        public LocalizationDefinitions Definitions => _definitions;

#if PFOUND_LOCALIZATION_QA
        /// <summary>
        /// Non-shipping QA overrides (show-keys / pseudo-localization). Compiled in only when
        /// <c>PFOUND_LOCALIZATION_QA</c> is defined, so shipping builds carry no diagnostic branch.
        /// </summary>
        public LocalizationDiagnostics Diagnostics { get; } = new LocalizationDiagnostics();
#endif

        // --- lifecycle passthrough ---
        public LanguageKey ActiveLanguage => _resolver.ActiveLanguage;
        public IReadOnlyList<LanguageKey> SupportedLanguages => _resolver.SupportedLanguages;
        public bool IsLanguageSupported(LanguageKey language) => _resolver.IsLanguageSupported(language);
        public void SwitchLanguage(LanguageKey language) => _resolver.SwitchLanguage(language);

        // --- ILocalizationContext ---
        public CultureInfo Culture => _culture;
        string ILocalizationContext.Localize(LocalizationKey key) => Get(key);

        // ---------------------------------------------------------------- lookup variants

        /// <summary>Localized, tag-processed text; the raw key string if the key is missing.</summary>
        public string Get(LocalizationKey key, params ILocalizationValue[] values)
        {
            if (TryResolveRaw(key, values, out string raw, out var binding, out _))
                return Finish(key, raw, binding, values);
            return key.Value;
        }

        /// <summary>True + processed text when found; false (and null text) when the key is missing.</summary>
        public bool TryGet(LocalizationKey key, out string text, params ILocalizationValue[] values)
        {
            if (TryResolveRaw(key, values, out string raw, out var binding, out _))
            {
                text = Finish(key, raw, binding, values);
                return true;
            }
            text = null;
            return false;
        }

        /// <summary>Fallback-aware lookup: also reports whether the value came from the fallback language.</summary>
        public bool TryGet(LocalizationKey key, out string text, out bool fromFallback, params ILocalizationValue[] values)
        {
            if (TryResolveRaw(key, values, out string raw, out var binding, out fromFallback))
            {
                text = Finish(key, raw, binding, values);
                return true;
            }
            text = null;
            fromFallback = false;
            return false;
        }

        /// <summary>Processed text, or throws <see cref="KeyNotFoundException"/> when the key is missing.</summary>
        public string GetEnsured(LocalizationKey key, params ILocalizationValue[] values)
        {
            if (TryResolveRaw(key, values, out string raw, out var binding, out _))
                return Finish(key, raw, binding, values);
            throw new KeyNotFoundException("Missing localization key: " + key.Value);
        }

        /// <summary>
        /// Processed text, or — when missing — logs an error and returns the visible placeholder
        /// <c># [key] #</c> so the gap is obvious in the UI.
        /// </summary>
        public string GetOrErrorText(LocalizationKey key, params ILocalizationValue[] values)
        {
            if (TryResolveRaw(key, values, out string raw, out var binding, out _))
                return Finish(key, raw, binding, values);
            LocalizationLog.Error("Missing localization key: " + key.Value);
            return "# [" + key.Value + "] #";
        }

        /// <summary>
        /// Tag-processes a resolved raw value, then — in non-shipping builds — applies any active QA
        /// override (show-keys / pseudo-localization). Shipping builds compile straight to <see cref="Process"/>.
        /// </summary>
        private string Finish(LocalizationKey key, string raw, ParameterDefinition binding, ILocalizationValue[] values)
        {
#if PFOUND_LOCALIZATION_QA
            if (Diagnostics.ShowKeys) return key.Value;
            string processed = Process(raw, binding, values);
            return Diagnostics.PseudoLocalize ? Diagnostics.Pseudoize(processed) : processed;
#else
            return Process(raw, binding, values);
#endif
        }

        // ---------------------------------------------------------------- resolution

        private bool TryResolveRaw(LocalizationKey key, ILocalizationValue[] values,
            out string raw, out ParameterDefinition binding, out bool fromFallback)
        {
            raw = null;
            binding = ParameterDefinition.Empty;
            fromFallback = false;
            if (!key.IsValid) return false;

            if (key.Value.IndexOf(LocalizationConstants.DynamicKeyMarker) >= 0)
            {
                binding = _definitions.TryGetDynamicKey(key.Value, out var dynDef) ? dynDef : ParameterDefinition.Empty;
                var concrete = new LocalizationKey(BuildConcreteKey(key.Value, binding, values));
                return _resolver.TryGetUnprocessed(concrete, out raw, out fromFallback);
            }

            binding = _definitions.ParametersFor(key.Value);
            return _resolver.TryGetUnprocessed(key, out raw, out fromFallback);
        }

        private string BuildConcreteKey(string template, ParameterDefinition binding, ILocalizationValue[] values)
        {
            int count = Math.Min(binding.Count, values != null ? values.Length : 0);
            if (count > MaxDynamicParameters)
            {
                LocalizationLog.Warn("Dynamic key '" + template + "' declares more than " + MaxDynamicParameters + " parameters; extra ignored.");
                count = MaxDynamicParameters;
            }
            string result = template;
            var sb = Scratch();
            for (int i = 0; i < count; i++)
            {
                sb.Length = 0;
                values[i].AppendTo(sb);
                string token = LocalizationConstants.TagOpen + binding[i].Name + LocalizationConstants.TagClose;
                result = result.Replace(token, sb.ToString());
            }
            return result;
        }

        private string Process(string text, ParameterDefinition binding, ILocalizationValue[] values)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf(LocalizationConstants.TagOpen) < 0)
                return text; // no tags: nothing to substitute

            var outcome = TagScanner.Process(
                text, LocalizationConstants.TagOpen, LocalizationConstants.TagClose,
                (tagName, output) => Substitute(tagName, binding, values, output),
                out string result);

            if (outcome != TagScanOutcome.Accepted)
            {
                LocalizationLog.Error("Malformed localization tags (" + outcome + ") in: " + text);
                return text;
            }
            return result;
        }

        private void Substitute(string tagName, ParameterDefinition binding, ILocalizationValue[] values, StringBuilder output)
        {
            int idx = binding.IndexOf(tagName);
            if (idx < 0 || values == null || idx >= values.Length)
            {
                LocalizationLog.Error("Unmatched localization tag: '" + tagName + "'");
                output.Append(tagName);
                return;
            }

            var value = values[idx];
            var spec = binding[idx];

            // (1) A value that maps to an existing localization key wins over generic formatting.
            if (TryResolveValueKey(value, out var valueKey) && _resolver.TryGetUnprocessed(valueKey, out _))
            {
                output.Append(Get(valueKey));
                return;
            }

            // (2) Format-type rendering (custom registry, built-ins, then the value's own fallback).
            _formatters.Format(value, spec.Format, this, output);
        }

        private bool TryResolveValueKey(ILocalizationValue value, out LocalizationKey key)
        {
            if (_valueKeyResolver != null && _valueKeyResolver.TryResolveKey(value, out key))
                return true;
            return value.TryGetLocalizationKey(out key);
        }

        private static StringBuilder Scratch() => t_scratch ?? (t_scratch = new StringBuilder(64));
    }
}
