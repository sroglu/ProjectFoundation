using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PFound.Render.UIShapes.Editor
{
    /// <summary>
    /// Pure-C# bake pipeline: renders the configured <c>UIShape</c> material into a
    /// <see cref="Texture2D"/> via <see cref="Graphics.Blit(Texture, RenderTexture, Material)"/>
    /// and optionally saves the result as a PNG with developer-controlled importer settings.
    /// Used by <see cref="UIShapeBakeWindow"/> + tested directly by EditMode tests.
    /// </summary>
    /// <remarks>
    /// The bake path does NOT allocate per-frame — each call grabs a <see cref="RenderTexture"/>
    /// from the temp pool, blits the material, reads pixels into a new <see cref="Texture2D"/>,
    /// and releases the RT.
    /// </remarks>
    public static class UIShapeBakeService
    {
        /// <summary>
        /// Renders <paramref name="material"/> into a <see cref="Texture2D"/> sized
        /// <c><see cref="BakeSettings.Width"/> × <see cref="BakeSettings.Height"/></c>.
        /// The material's <c>_RectSize</c> property is overwritten to match the chosen output.
        /// </summary>
        /// <param name="material">Material using the <c>Render/UI/Shape</c> shader. Required.</param>
        /// <param name="settings">Bake configuration.</param>
        /// <returns>A newly-allocated <see cref="Texture2D"/>. Caller owns disposal.</returns>
        /// <exception cref="ArgumentNullException">When <paramref name="material"/> is <c>null</c>.</exception>
        public static Texture2D Bake(Material material, in BakeSettings settings)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));

            int w = Mathf.Clamp(settings.Width, 16, 8192);
            int h = Mathf.Clamp(settings.Height, 16, 8192);

            // Stamp the output size into the material so the shader's SDF sees the bake resolution.
            // For bake, the quad == shape — no margin needed (bake captures the shape exactly).
            // Shadow-in-bake consumers can pre-set _QuadSize > _RectSize manually before bake.
            material.SetVector(UIShapeMaterialProperties.RectSize, new Vector4(w, h, 0f, 0f));
            material.SetVector(UIShapeMaterialProperties.QuadSize, new Vector4(w, h, 0f, 0f));

            var readWrite = settings.ColorSpace == BakeColorSpace.Linear
                ? RenderTextureReadWrite.Linear
                : RenderTextureReadWrite.sRGB;
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, readWrite);
            var textureFormat = TextureFormat.RGBA32;
            var mipChain = false;
            var linear = settings.ColorSpace == BakeColorSpace.Linear;
            var texture = new Texture2D(w, h, textureFormat, mipChain, linear)
            {
                filterMode = settings.FilterMode,
                wrapMode = settings.WrapMode,
            };

            var prev = RenderTexture.active;
            try
            {
                Graphics.Blit(null, rt, material);
                RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                texture.Apply(mipChain, makeNoLongerReadable: false);
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }

            return texture;
        }

        /// <summary>
        /// Writes <paramref name="texture"/> to <paramref name="assetPath"/> as PNG and configures
        /// the <see cref="TextureImporter"/> from <paramref name="settings"/> (target type,
        /// color space, filter, wrap, sprite-mesh).
        /// </summary>
        /// <param name="texture">Texture produced by <see cref="Bake"/> (or compatible).</param>
        /// <param name="assetPath">Asset path under <c>Assets/</c>. Parent directory will be created if missing.</param>
        /// <param name="settings">Bake configuration (target type, importer fields, etc.).</param>
        /// <returns><c>true</c> on success.</returns>
        /// <exception cref="ArgumentNullException">When <paramref name="texture"/> or <paramref name="assetPath"/> is <c>null</c>.</exception>
        public static bool Save(Texture2D texture, string assetPath, in BakeSettings settings)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));
            if (string.IsNullOrEmpty(assetPath)) throw new ArgumentNullException(nameof(assetPath));

            // Ensure parent directory exists.
            string dir = Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            byte[] png = texture.EncodeToPNG();
            File.WriteAllBytes(assetPath, png);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("[UIShapes] Failed to configure importer at " + assetPath + " — TextureImporter not found.");
                return false;
            }

            importer.textureType = settings.TargetType == BakeTargetType.Sprite
                ? TextureImporterType.Sprite
                : TextureImporterType.Default;
            importer.sRGBTexture = settings.ColorSpace == BakeColorSpace.SRgb;
            importer.alphaIsTransparency = true;
            importer.filterMode = settings.FilterMode;
            importer.wrapMode = settings.WrapMode;
            importer.spriteImportMode = settings.TargetType == BakeTargetType.Sprite
                ? SpriteImportMode.Single
                : SpriteImportMode.None;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;

            var spriteSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(spriteSettings);
            spriteSettings.spriteMeshType = settings.GenerateSpriteMesh ? SpriteMeshType.Tight : SpriteMeshType.FullRect;
            importer.SetTextureSettings(spriteSettings);

            importer.SaveAndReimport();
            return true;
        }
    }
}
