using System.Collections.Generic;
using PFound.AssetPipeline.Core;
using UnityEditor;
using UnityEngine;

namespace PFound.AssetPipeline.Editor
{
    /// <summary>
    /// Finds distinct texture FILES whose pixel content is identical — the same image shipped under several paths.
    /// It reads each texture's <see cref="Texture2D.imageContentsHash"/> (a hash of the decoded pixels, independent
    /// of import settings) and delegates the grouping to the pure <see cref="DuplicateTextureFinder"/>. This is a
    /// different waste than the cross-bundle duplicate-DEPENDENCY check (one asset pulled into many bundles): there
    /// the files are the same asset; here the files differ but the content collides.
    /// </summary>
    public static class TextureContentDuplicateAnalyzer
    {
        /// <summary>Groups the given texture paths by identical pixel content (paths with no loadable Texture2D are skipped).</summary>
        public static List<DuplicateTextureGroup> Analyze(IEnumerable<string> texturePaths)
        {
            var pairs = new List<KeyValuePair<string, string>>();
            var seen = new HashSet<string>();
            foreach (string path in texturePaths)
            {
                if (string.IsNullOrEmpty(path) || !seen.Add(path)) continue;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) continue;
                pairs.Add(new KeyValuePair<string, string>(path, tex.imageContentsHash.ToString()));
            }
            return DuplicateTextureFinder.Find(pairs);
        }

        /// <summary>Scans every <c>t:Texture2D</c> in the project for content-identical duplicates.</summary>
        public static List<DuplicateTextureGroup> AnalyzeProject()
        {
            var paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D"))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            return Analyze(paths);
        }
    }
}
