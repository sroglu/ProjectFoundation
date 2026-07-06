using System.Collections.Generic;
using System.IO;
using System.Linq;
using PFound.UISystem.Shapes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace PFound.UISystem.Editor.Shapes
{
    /// <summary>
    /// Custom inspector for <see cref="ThemeMaterialRegistry"/> that adds three authoring
    /// actions on top of the default serialized fields: <b>Duplicate</b> (copy as new asset),
    /// <b>Discover keys</b> (scan the active scene for <c>ThemedSdfPanel</c> theme-keys and
    /// append any missing entries), and <b>Compare</b> (side-by-side diff against another
    /// registry).
    /// </summary>
    /// <remarks>
    /// All three are designer-facing — no runtime impact. The compare panel is rendered
    /// inline below the default inspector, so opening two inspector windows isn't necessary.
    /// </remarks>
    [CustomEditor(typeof(ThemeMaterialRegistry))]
    internal sealed class ThemeMaterialRegistryEditor : UnityEditor.Editor
    {
        private ThemeMaterialRegistry _other;
        private bool _showCompare;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Authoring Actions", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Duplicate as new asset…")) DuplicateAsNewAsset();
                if (GUILayout.Button("Discover keys from active scene")) DiscoverKeysFromScene();
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Compare", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _other = (ThemeMaterialRegistry)EditorGUILayout.ObjectField(
                    "Other registry", _other, typeof(ThemeMaterialRegistry), false);
                using (new EditorGUI.DisabledScope(_other == null || _other == target))
                {
                    if (GUILayout.Button(_showCompare ? "Hide diff" : "Show diff", GUILayout.Width(90)))
                        _showCompare = !_showCompare;
                }
            }

            if (_showCompare && _other != null && _other != target)
                DrawCompareTable((ThemeMaterialRegistry)target, _other);
        }

        // ----------------------------------------------------------------- //
        //  Duplicate                                                          //
        // ----------------------------------------------------------------- //

        private void DuplicateAsNewAsset()
        {
            var src = (ThemeMaterialRegistry)target;
            var srcPath = AssetDatabase.GetAssetPath(src);
            if (string.IsNullOrEmpty(srcPath))
            {
                EditorUtility.DisplayDialog("Duplicate",
                    "Cannot duplicate — source is not a saved asset on disk.", "OK");
                return;
            }

            var dir = Path.GetDirectoryName(srcPath) ?? "Assets";
            var defaultName = Path.GetFileNameWithoutExtension(srcPath) + "_Copy.asset";
            var dstPath = EditorUtility.SaveFilePanelInProject(
                "Duplicate Theme Registry", defaultName, "asset",
                "Choose a location for the new theme registry.", dir);
            if (string.IsNullOrEmpty(dstPath)) return;

            if (!AssetDatabase.CopyAsset(srcPath, dstPath))
            {
                EditorUtility.DisplayDialog("Duplicate",
                    $"AssetDatabase.CopyAsset failed: {srcPath} → {dstPath}", "OK");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var copy = AssetDatabase.LoadAssetAtPath<ThemeMaterialRegistry>(dstPath);
            if (copy != null)
            {
                // Reset ThemeName to file stem so it isn't a confusing duplicate string.
                var so = new SerializedObject(copy);
                so.FindProperty("ThemeName").stringValue = Path.GetFileNameWithoutExtension(dstPath);
                so.ApplyModifiedPropertiesWithoutUndo();
                Selection.activeObject = copy;
            }
            Debug.Log($"[ThemeMaterialRegistry] Duplicated '{srcPath}' → '{dstPath}'.");
        }

        // ----------------------------------------------------------------- //
        //  Auto-discover                                                       //
        // ----------------------------------------------------------------- //

        private void DiscoverKeysFromScene()
        {
            var sceneKeys = CollectThemeKeysFromOpenScenes();
            if (sceneKeys.Count == 0)
            {
                EditorUtility.DisplayDialog("Discover Keys",
                    "No ThemedSdfPanel theme-keys were found in the active scene(s).", "OK");
                return;
            }

            var registry = (ThemeMaterialRegistry)target;
            var entriesProp = serializedObject.FindProperty("_entries");
            var existing = new HashSet<string>();
            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                var k = entriesProp.GetArrayElementAtIndex(i).FindPropertyRelative("Key").stringValue;
                if (!string.IsNullOrEmpty(k)) existing.Add(k);
            }

            var added = new List<string>();
            foreach (var key in sceneKeys)
            {
                if (existing.Contains(key)) continue;
                entriesProp.arraySize++;
                var newEntry = entriesProp.GetArrayElementAtIndex(entriesProp.arraySize - 1);
                newEntry.FindPropertyRelative("Key").stringValue = key;
                newEntry.FindPropertyRelative("Material").objectReferenceValue = null;
                added.Add(key);
            }

            serializedObject.ApplyModifiedProperties();
            if (added.Count > 0)
            {
                EditorUtility.SetDirty(registry);
                Debug.Log($"[ThemeMaterialRegistry] Discovered {added.Count} new key(s) on '{registry.name}': {string.Join(", ", added)}. Materials are unset — fill them in.");
            }
            else
            {
                Debug.Log($"[ThemeMaterialRegistry] All {sceneKeys.Count} scene theme-key(s) are already present on '{registry.name}'.");
            }
        }

        private static HashSet<string> CollectThemeKeysFromOpenScenes()
        {
            var keys = new HashSet<string>();
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.IsValid() || !scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var doc in root.GetComponentsInChildren<UIDocument>(true))
                    {
                        if (doc.visualTreeAsset == null) continue;
                        var template = doc.visualTreeAsset.CloneTree();
                        template.Query<ThemedSdfPanel>().ForEach(p =>
                        {
                            if (!string.IsNullOrEmpty(p.ThemeKey)) keys.Add(p.ThemeKey);
                        });
                    }
                }
            }
            return keys;
        }

        // ----------------------------------------------------------------- //
        //  Compare table                                                       //
        // ----------------------------------------------------------------- //

        private static void DrawCompareTable(ThemeMaterialRegistry a, ThemeMaterialRegistry b)
        {
            var aKeys = a.Keys.ToHashSet();
            var bKeys = b.Keys.ToHashSet();
            var union = new SortedSet<string>(aKeys);
            union.UnionWith(bKeys);

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Key", EditorStyles.boldLabel, GUILayout.Width(160));
                    EditorGUILayout.LabelField(a.name, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(b.name, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Status", EditorStyles.boldLabel, GUILayout.Width(120));
                }

                int onlyA = 0, onlyB = 0, both = 0, materialDiff = 0;
                foreach (var key in union)
                {
                    var inA = aKeys.Contains(key);
                    var inB = bKeys.Contains(key);
                    var matA = inA ? a.Get(key) : null;
                    var matB = inB ? b.Get(key) : null;
                    string status;
                    if (!inA) { status = "only B"; onlyB++; }
                    else if (!inB) { status = "only A"; onlyA++; }
                    else if (matA != matB) { status = "different"; materialDiff++; both++; }
                    else { status = "match"; both++; }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(key, GUILayout.Width(160));
                        EditorGUILayout.ObjectField(matA, typeof(Material), false);
                        EditorGUILayout.ObjectField(matB, typeof(Material), false);
                        EditorGUILayout.LabelField(status, GUILayout.Width(120));
                    }
                }

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField(
                    $"Summary: {both} shared ({materialDiff} differ), {onlyA} only in A, {onlyB} only in B.",
                    EditorStyles.miniLabel);
            }
        }
    }
}
