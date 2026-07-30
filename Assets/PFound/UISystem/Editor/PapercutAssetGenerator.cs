using System.IO;
using UnityEditor;
using UnityEngine;

namespace PFound.UISystem.Editor.Themes.Papercut
{
    /// <summary>
    /// One-shot generator for the Papercut highlight curve PNG. Produces a
    /// 32×32 alpha-only quarter-arc sticker highlight next to the theme's
    /// USS files with the import settings the theme expects (point filter,
    /// alpha-only, no mips).
    ///
    /// Run via the menu OR programmatically:
    ///   PFound.UISystem.Editor.Themes.Papercut.PapercutAssetGenerator.GenerateHighlightCurve();
    /// </summary>
    public static class PapercutAssetGenerator
    {
        private const string OutputPath =
            "Assets/PFound/UISystem/Styles/Themes/Papercut/highlight-curve.png";
        private const int Size = 32;

        [MenuItem("Tools/Papercut/Generate Highlight Curve")]
        public static void GenerateHighlightCurve()
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, false);
            var pixels = new Color32[Size * Size];

            // Bold sticker-shine crescent. A thick annulus segment at the
            // 10-o'clock position with mostly-solid alpha in the body and
            // soft tapered ends.
            var centre = new Vector2(Size * 0.5f, Size * 0.5f);
            const float innerR = 7f;
            const float outerR = 15f;
            const float radialFeather = 1.5f;
            const float angleStart =  90f * Mathf.Deg2Rad;
            const float angleEnd   = 170f * Mathf.Deg2Rad;
            const float angleFade  =  10f * Mathf.Deg2Rad;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    var off = p - centre;
                    var dist = off.magnitude;
                    var angle = Mathf.Atan2(off.y, off.x);

                    float r;
                    if (dist < innerR - radialFeather || dist > outerR + radialFeather) r = 0f;
                    else if (dist < innerR) r = (dist - (innerR - radialFeather)) / radialFeather;
                    else if (dist > outerR) r = 1f - (dist - outerR) / radialFeather;
                    else r = 1f;

                    float a;
                    if (angle < angleStart || angle > angleEnd) a = 0f;
                    else if (angle < angleStart + angleFade) a = (angle - angleStart) / angleFade;
                    else if (angle > angleEnd - angleFade) a = (angleEnd - angle) / angleFade;
                    else a = 1f;

                    var alpha = Mathf.Clamp01(r * a);
                    var byteA = (byte)Mathf.RoundToInt(alpha * 255f);
                    pixels[y * Size + x] = new Color32(255, 255, 255, byteA);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            var dir = Path.GetDirectoryName(OutputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllBytes(OutputPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(OutputPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.spriteImportMode = SpriteImportMode.None;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.isReadable = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            Debug.Log($"[Papercut] Highlight curve generated at {OutputPath}");
        }
    }
}
