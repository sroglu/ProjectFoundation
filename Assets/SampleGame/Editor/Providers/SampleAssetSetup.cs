using System.Reflection;
using UnityEditor;

namespace PFound.SampleGame.Editor
{
    /// <summary>
    /// Shared authoring helpers for the sample-game providers: reflection assignment for private
    /// serialized fields (mirrors UISystem's <c>DefaultAssetsSetup.SetField</c>) and folder creation.
    /// Editor-only, ProjectFoundation-scoped — never shipped to a consumer that pulls a single module.
    /// </summary>
    internal static class SampleAssetSetup
    {
        /// <summary>Root every generated sample asset lands under.</summary>
        public const string GameSpecificRoot = "Assets/GameSpecific";

        /// <summary>Assigns a private serialized <paramref name="fieldName"/> on <paramref name="target"/>.</summary>
        public static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(target, value);
        }

        /// <summary>Creates <paramref name="path"/> and every missing parent folder under Assets/.</summary>
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string child = System.IO.Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
