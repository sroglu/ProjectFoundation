using System.Text;

namespace PFound.LocalizationService
{
    /// <summary>
    /// Build-gated QA diagnostics layered over the resolution facade. Two independent, off-by-default
    /// stress modes for catching localization defects before shipping:
    /// <list type="bullet">
    /// <item>show-keys — return the raw key instead of the translation, making missing / mis-wired key
    /// usage immediately visible in the UI.</item>
    /// <item>pseudo-localization — remap ASCII letters to accented look-alikes (surfaces non-Latin
    /// glyph / font-coverage issues) and pad the string by <see cref="ExpansionFactor"/> (surfaces
    /// text-overflow / truncation in tight layouts).</item>
    /// </list>
    /// This type is engine-free and deterministic so it can be unit-tested directly. The facade only
    /// consults it in non-shipping builds (behind a compile gate), so shipping output is never altered.
    /// </summary>
    public sealed class LocalizationDiagnostics
    {
        /// <summary>Padding glyph appended to reach the expanded length.</summary>
        private const char PadChar = '~';

        private float _expansionFactor = 0.4f;

        /// <summary>When set, a lookup returns the key text verbatim instead of its translation.</summary>
        public bool ShowKeys { get; set; }

        /// <summary>When set (and <see cref="ShowKeys"/> is off), lookups are pseudo-localized.</summary>
        public bool PseudoLocalize { get; set; }

        /// <summary>
        /// Fractional length increase applied by pseudo-localization (0.4 = +40%). Clamped to non-negative.
        /// </summary>
        public float ExpansionFactor
        {
            get => _expansionFactor;
            set => _expansionFactor = value < 0f ? 0f : value;
        }

        /// <summary>
        /// Applies the active QA mode to a resolved lookup: the key when <see cref="ShowKeys"/> is set, a
        /// pseudo-localized string when <see cref="PseudoLocalize"/> is set, otherwise the text unchanged.
        /// </summary>
        public string Apply(LocalizationKey key, string localized)
        {
            if (ShowKeys) return key.Value;
            if (PseudoLocalize) return Pseudoize(localized);
            return localized;
        }

        /// <summary>
        /// Remaps ASCII letters to accented look-alikes and pads the result to <c>length * (1 + factor)</c>
        /// with a filler glyph. Deterministic: the same input always yields the same output.
        /// </summary>
        public string Pseudoize(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var sb = new StringBuilder(text.Length + 8);
            for (int i = 0; i < text.Length; i++)
                sb.Append(Accent(text[i]));

            int target = (int)(text.Length * (1f + _expansionFactor));
            while (sb.Length < target)
                sb.Append(PadChar);

            return sb.ToString();
        }

        /// <summary>Deterministic ASCII-letter to accented look-alike mapping; non-letters pass through.</summary>
        private static char Accent(char c)
        {
            switch (c)
            {
                case 'a': return 'á'; case 'b': return 'ƀ'; case 'c': return 'ç'; case 'd': return 'đ';
                case 'e': return 'é'; case 'f': return 'ƒ'; case 'g': return 'ğ'; case 'h': return 'ĥ';
                case 'i': return 'í'; case 'j': return 'ĵ'; case 'k': return 'ķ'; case 'l': return 'ĺ';
                case 'm': return 'ɱ'; case 'n': return 'ñ'; case 'o': return 'ó'; case 'p': return 'þ';
                case 'q': return 'ɋ'; case 'r': return 'ŕ'; case 's': return 'š'; case 't': return 'ť';
                case 'u': return 'ú'; case 'v': return 'ʋ'; case 'w': return 'ŵ'; case 'x': return 'ẋ';
                case 'y': return 'ý'; case 'z': return 'ž';
                case 'A': return 'Á'; case 'B': return 'Ɓ'; case 'C': return 'Ç'; case 'D': return 'Đ';
                case 'E': return 'É'; case 'F': return 'Ƒ'; case 'G': return 'Ğ'; case 'H': return 'Ĥ';
                case 'I': return 'Í'; case 'J': return 'Ĵ'; case 'K': return 'Ķ'; case 'L': return 'Ĺ';
                case 'M': return 'Ḿ'; case 'N': return 'Ñ'; case 'O': return 'Ó'; case 'P': return 'Þ';
                case 'Q': return 'Ɋ'; case 'R': return 'Ŕ'; case 'S': return 'Š'; case 'T': return 'Ť';
                case 'U': return 'Ú'; case 'V': return 'Ʋ'; case 'W': return 'Ŵ'; case 'X': return 'Ẋ';
                case 'Y': return 'Ý'; case 'Z': return 'Ž';
                default: return c;
            }
        }
    }
}
