using UnityEditor;
using UnityEngine;

namespace PFound.Utilities.EditorHelpers
{
    /// <summary>
    /// Default Inspector for <see cref="ImportableAsset"/>: draws the asset's
    /// serialized fields followed by an Import button. Marked
    /// <c>editorForChildClasses</c> so any subclass without its own custom
    /// editor automatically inherits the button.
    ///
    /// Subclassed editors that need conditional field rendering can derive from
    /// this class and call <see cref="DrawImportButton"/> at the end of their
    /// own <c>OnInspectorGUI</c>.
    /// </summary>
    [CustomEditor(typeof(ImportableAsset), editorForChildClasses: true)]
    public class ImportableAssetEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            DrawImportButton(target as ImportableAsset);
        }

        /// <summary>
        /// Renders the Import button (and a HelpBox with
        /// <see cref="ImportableAsset.CannotImportReason"/> when disabled).
        /// Safe to call from any custom editor whose target derives from
        /// <see cref="ImportableAsset"/>.
        /// </summary>
        public static void DrawImportButton(ImportableAsset asset)
        {
            if (asset == null) return;

            EditorGUILayout.Space(10);

            if (!asset.CanImport && !string.IsNullOrEmpty(asset.CannotImportReason))
                EditorGUILayout.HelpBox(asset.CannotImportReason, MessageType.Warning);

            using (new EditorGUI.DisabledScope(!asset.CanImport))
            {
                if (GUILayout.Button("Import", GUILayout.Height(28)))
                {
                    try { asset.Import(); }
                    catch (System.Exception ex) { Debug.LogException(ex); }
                }
            }
        }
    }
}
