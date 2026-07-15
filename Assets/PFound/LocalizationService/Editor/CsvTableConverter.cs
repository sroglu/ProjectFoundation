using System.Collections.Generic;
using System.IO;
using System.Text;
using PFound.Compression;

namespace PFound.LocalizationService.EditorTools
{
    /// <summary>
    /// Editor entry point that converts a localization CSV into the four on-disk table files: the
    /// definitions and content texts each written both plain (<c>.txt</c>) and LZMA-compressed
    /// (<c>.lzma</c>, via the shared PFound.Compression codec). Enum keys are auto-included by scanning the
    /// loaded assemblies for <c>[LocalizableEnum]</c> types. The routing/parse work is done by the
    /// engine-free <see cref="LocalizationTableBuilder"/>.
    /// </summary>
    public static class CsvTableConverter
    {
        /// <summary>Converts the CSV at <paramref name="csvPath"/> and writes the four files into <paramref name="outputDir"/>.</summary>
        public static void Convert(string csvPath, string outputDir, bool includeEnums = true)
        {
            string csv = File.ReadAllText(csvPath);
            var enums = includeEnums ? LocalizableEnumScanner.Scan() : new List<System.Type>();
            var result = LocalizationTableBuilder.Build(csv, enums);
            WriteFiles(outputDir, result);
        }

        public static void WriteFiles(string outputDir, LocalizationTableBuilder.Result result)
        {
            Directory.CreateDirectory(outputDir);
            WritePair(outputDir, LocalizationConstants.DefinitionsFilePrefix, result.DefinitionsText);
            WritePair(outputDir, LocalizationConstants.ContentFilePrefix, result.ContentText);
        }

        private static void WritePair(string dir, string prefix, string text)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(text);
            File.WriteAllBytes(Path.Combine(dir, prefix + LocalizationConstants.PlainExtension), utf8);
            File.WriteAllBytes(Path.Combine(dir, prefix + LocalizationConstants.CompressedExtension), Lzma.Compress(utf8));
        }
    }
}
