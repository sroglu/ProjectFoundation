using System.IO;
using System.Text;
using UnityEngine;
using PFound.Compression;

namespace PFound.LocalizationService.Unity
{
    /// <summary>
    /// Loads localization table text from the engine's file locations. Plain <c>.txt</c> tables are read
    /// directly; compressed <c>.lzma</c> tables are inflated with the shared PFound.Compression codec
    /// (never a re-vendored 7-zip). Resolves the <c>Localizables</c> folder under either
    /// <see cref="Application.streamingAssetsPath"/> or <see cref="Application.persistentDataPath"/>.
    /// </summary>
    public static partial class TableFileLoader
    {
        public static string StreamingAssetsTablesDir
            => Path.Combine(Application.streamingAssetsPath, LocalizationConstants.TablesFolderName);

        public static string PersistentTablesDir
            => Path.Combine(Application.persistentDataPath, LocalizationConstants.TablesFolderName);

        /// <summary>Reads a table file, decompressing if it carries the compressed extension.</summary>
        public static string ReadTable(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (path.EndsWith(LocalizationConstants.CompressedExtension, System.StringComparison.OrdinalIgnoreCase))
                bytes = Lzma.Decompress(bytes);
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// Builds a directory-backed table set from a folder, transparently handling either the plain or
        /// the compressed variant of each file.
        /// </summary>
        public static (LocalizationDefinitions definitions, InMemoryLocalizationSource content) LoadFrom(string directory)
        {
            string defsText = ReadPrefixed(directory, LocalizationConstants.DefinitionsFilePrefix);
            string contentText = ReadPrefixed(directory, LocalizationConstants.ContentFilePrefix);

            var definitions = defsText != null
                ? LocalizationDefinitions.FromIni(IniDocument.Parse(defsText))
                : new LocalizationDefinitions();
            var content = contentText != null
                ? ContentTables.FromText(contentText)
                : new InMemoryLocalizationSource();

            return (definitions, content);
        }

        private static string ReadPrefixed(string directory, string prefix)
        {
            // Prefer plain over compressed; accept an optional "-suffix" variant.
            foreach (var ext in new[] { LocalizationConstants.PlainExtension, LocalizationConstants.CompressedExtension })
            {
                string exact = Path.Combine(directory, prefix + ext);
                if (File.Exists(exact)) return ReadTable(exact);
                if (Directory.Exists(directory))
                    foreach (var path in Directory.GetFiles(directory, prefix + "*" + ext))
                        return ReadTable(path);
            }
            return null;
        }
    }
}
