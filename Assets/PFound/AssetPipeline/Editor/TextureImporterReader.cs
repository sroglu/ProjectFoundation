using System.Collections.Generic;
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
        /// <summary>The build targets whose per-platform texture overrides the audit inspects.</summary>
        public static readonly string[] InspectedPlatforms = { "iOS", "Android" };

        /// <summary>
        /// Reads <paramref name="assetPath"/> as a texture, or returns null when the asset has no
        /// <see cref="TextureImporter"/> (i.e. it is not a texture). The default-platform (top-level) settings feed
        /// the default rules; each of <see cref="InspectedPlatforms"/> is read via <c>GetPlatformTextureSettings</c>
        /// so an overridden iOS/Android target is audited on its own settings.
        /// </summary>
        public static TextureImporterFacts Read(string assetPath)
        {
            if (!(AssetImporter.GetAtPath(assetPath) is TextureImporter importer)) return null;

            importer.GetSourceTextureWidthAndHeight(out int width, out int height);

            var overrides = new List<PlatformTextureFacts>(InspectedPlatforms.Length);
            foreach (string platform in InspectedPlatforms)
            {
                var ps = importer.GetPlatformTextureSettings(platform);
                overrides.Add(new PlatformTextureFacts
                {
                    Platform = platform,
                    Overridden = ps.overridden,
                    Compression = MapCompression(ps.textureCompression),
                    Crunched = ps.crunchedCompression,
                    MaxTextureSize = ps.maxTextureSize,
                    FormatName = ps.format.ToString(),
                    CompressionQuality = ps.compressionQuality,
                });
            }

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
                PlatformOverrides = overrides,
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
