using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PFound.TweenPresetLibrary.Editor
{
    /// <summary>
    /// Editor authoring tools for tween presets: create a new asset, open the preview window, and normalize selected
    /// preset asset names to a stable convention. Editor-only; the runtime stays free of editor concerns.
    /// </summary>
    public static class TweenPresetEditorTools
    {
        [MenuItem("PFound/Tween Preset Library/Create Tween Preset")]
        public static void CreatePreset()
        {
            var asset = ScriptableObject.CreateInstance<TweenPreset>();
            ProjectWindowUtil.CreateAsset(asset, "TweenPreset.asset"); // drops into the active folder, ready to rename
        }

        [MenuItem("PFound/Tween Preset Library/Open Preview Window")]
        public static void OpenPreview() => TweenPresetPreviewWindow.Open();

        [MenuItem("PFound/Tween Preset Library/Normalize Selected Preset Name(s)")]
        public static void NormalizeSelectedNames()
        {
            int renamed = 0;
            foreach (var obj in Selection.objects)
            {
                if (!(obj is TweenPreset)) continue;
                string path = AssetDatabase.GetAssetPath(obj);
                string current = Path.GetFileNameWithoutExtension(path);
                string normalized = NormalizeName(current);
                if (!string.IsNullOrEmpty(normalized) && normalized != current)
                {
                    AssetDatabase.RenameAsset(path, normalized);
                    renamed++;
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[TweenPresetLibrary] Normalized {renamed} preset name(s).");
        }

        /// <summary>
        /// A stable preset-name convention: lower-case, trimmed, spaces/dashes collapsed to single underscores, no
        /// leading/trailing underscore. Pure (no Unity) so it is unit-testable.
        /// </summary>
        public static string NormalizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;

            var sb = new StringBuilder(name.Length);
            bool lastUnderscore = false;
            foreach (char c in name.Trim().ToLowerInvariant())
            {
                char mapped = (c == ' ' || c == '-') ? '_' : c;
                if (mapped == '_')
                {
                    if (lastUnderscore) continue; // collapse runs
                    lastUnderscore = true;
                }
                else lastUnderscore = false;
                sb.Append(mapped);
            }
            return sb.ToString().Trim('_');
        }
    }
}
