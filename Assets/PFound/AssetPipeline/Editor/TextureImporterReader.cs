using PFound.AssetPipeline.Core;
using UnityEditor;

namespace PFound.AssetPipeline.Editor
{
    /// <summary>
    /// Translates a Unity <see cref="TextureImporter"/> into engine-free <see cref="TextureImporterFacts"/> for the
    /// policy evaluator. Reads the importer's settings only — never mutates. The <see cref="AssetOptimizer"/> is the
    /// (separate, opt-in) side that writes settings back.
    /// </summary>
    public static class TextureImporterReader
    {
        /// <summary>
        /// Reads <paramref name="assetPath"/> as a texture, or returns null when the asset has no
        /// <see cref="TextureImporter"/> (i.e. it is not a texture). Compression/crunch/size/NPOT are read from the
        /// importer's top-level (default-platform) properties; the effective format name comes from the default
        /// platform settings. Per-platform overrides (iOS/Android) are reliable but not yet inspected here — a
        /// deferred capability (see STATUS).
        /// </summary>
        public static TextureImporterFacts Read(string assetPath)
        {
            if (!(AssetImporter.GetAtPath(assetPath) is TextureImporter importer)) return null;

            importer.GetSourceTextureWidthAndHeight(out int width, out int height);

            return new TextureImporterFacts
            {
                AssetPath = assetPath,
                Width = width,
                Height = height,
                Compression = MapCompression(importer.textureCompression),
                Crunched = importer.crunchedCompression,
                MaxTextureSize = importer.maxTextureSize,
                NpotScale = MapNpotScale(importer.npotScale),
                IsSprite = importer.textureType == TextureImporterType.Sprite,
                MipmapsEnabled = importer.mipmapEnabled,
                ReadWriteEnabled = importer.isReadable,
                // No per-entry "requires Read/Write" opt-out is wired yet; default to flagging Read/Write (mirrors mesh).
                ReadWriteRequired = false,
                FormatName = importer.GetDefaultPlatformTextureSettings().format.ToString(),
            };
        }

        private static TextureCompressionLevel MapCompression(TextureImporterCompression c)
        {
            switch (c)
            {
                case TextureImporterCompression.Uncompressed: return TextureCompressionLevel.Uncompressed;
                case TextureImporterCompression.CompressedLQ: return TextureCompressionLevel.LowQuality;
                case TextureImporterCompression.CompressedHQ: return TextureCompressionLevel.HighQuality;
                default: return TextureCompressionLevel.Normal; // TextureImporterCompression.Compressed
            }
        }

        private static NpotScale MapNpotScale(TextureImporterNPOTScale s)
        {
            switch (s)
            {
                case TextureImporterNPOTScale.ToNearest: return NpotScale.ToNearest;
                case TextureImporterNPOTScale.ToLarger: return NpotScale.ToLarger;
                case TextureImporterNPOTScale.ToSmaller: return NpotScale.ToSmaller;
                default: return NpotScale.None;
            }
        }
    }
}
