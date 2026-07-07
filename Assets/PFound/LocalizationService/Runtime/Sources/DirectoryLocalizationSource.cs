using System;
using System.Collections.Generic;
using System.IO;

namespace PFound.LocalizationService
{
    /// <summary>
    /// Reads localization tables from a folder of plain-text INI files. The definitions file
    /// (<c>LocalizationDefinitions[-…].txt</c>) is loaded eagerly to learn the supported languages and
    /// parameter metadata; the content file (<c>LocalizationText[-…].txt</c>) is parsed lazily per
    /// language. Uses only <see cref="System.IO"/> (no engine dependency) so it is mono-testable; the
    /// Unity layer supplies StreamingAssets/persistentDataPath directories and, for compressed tables, a
    /// decompressing variant.
    /// </summary>
    public sealed class DirectoryLocalizationSource : ILocalizationSource
    {
        private readonly string _directory;
        private InMemoryLocalizationSource _content;
        private LocalizationDefinitions _definitions;

        public DirectoryLocalizationSource(string directory)
        {
            _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        }

        public LocalizationDefinitions Definitions
        {
            get { EnsureLoaded(); return _definitions; }
        }

        public IReadOnlyList<LanguageKey> AvailableLanguages
        {
            get { EnsureLoaded(); return _content.AvailableLanguages; }
        }

        public IReadOnlyDictionary<string, string> Load(LanguageKey language)
        {
            EnsureLoaded();
            return _content.Load(language);
        }

        private void EnsureLoaded()
        {
            if (_content != null) return;

            string defsFile = FindFile(LocalizationConstants.DefinitionsFilePrefix);
            _definitions = defsFile != null
                ? LocalizationDefinitions.FromIni(IniDocument.Parse(File.ReadAllText(defsFile)))
                : new LocalizationDefinitions();

            string contentFile = FindFile(LocalizationConstants.ContentFilePrefix);
            _content = contentFile != null
                ? ContentTables.FromText(File.ReadAllText(contentFile))
                : new InMemoryLocalizationSource();
        }

        private string FindFile(string prefix)
        {
            string exact = Path.Combine(_directory, prefix + LocalizationConstants.PlainExtension);
            if (File.Exists(exact)) return exact;

            // Fall back to a prefixed variant, e.g. "LocalizationText-project.txt".
            foreach (var path in Directory.GetFiles(_directory, prefix + "*" + LocalizationConstants.PlainExtension))
                return path;
            return null;
        }

        /// <summary>Force a reload on next access (editor/runtime unload).</summary>
        public void Unload() { _content = null; _definitions = null; }
    }
}
