using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PFound.UISystem.Editor.PageBuilder
{
    /// <summary>
    /// Page Builder — M3 component palette that works with Unity's native UI Builder.
    /// Creates UXML pages with correct M3 style references and adds components to them.
    /// Open via: Game Tools > Page Builder
    /// </summary>
    public sealed class PageBuilderWindow : EditorWindow
    {
        [MenuItem("Game Tools/Page Builder")]
        public static void ShowWindow()
        {
            var window = GetWindow<PageBuilderWindow>("Page Builder");
            window.minSize = new Vector2(300f, 400f);
            window.Show();
        }

        private ComponentPalette _palette;
        private string _activeUxmlPath;

        private void OnEnable()
        {
            _palette = new ComponentPalette();
            _palette.OnComponentSelected += OnComponentSelected;

            // Try to detect currently open UXML
            DetectActiveUxml();
        }

        private void OnDisable()
        {
            if (_palette != null)
                _palette.OnComponentSelected -= OnComponentSelected;
        }

        private void OnFocus()
        {
            DetectActiveUxml();
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space(4f);
            DrawActiveFileInfo();
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            _palette.OnGUI();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUILayout.LabelField("Page Builder", EditorStyles.boldLabel, GUILayout.Width(100f));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("New Page", EditorStyles.toolbarButton, GUILayout.Width(75f)))
                CreateNewPage();

            if (GUILayout.Button("Open", EditorStyles.toolbarButton, GUILayout.Width(50f)))
                OpenExistingPage();

            GUI.enabled = !string.IsNullOrEmpty(_activeUxmlPath);

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50f)))
                ClearPage();

            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }

        private void DrawActiveFileInfo()
        {
            if (string.IsNullOrEmpty(_activeUxmlPath))
            {
                EditorGUILayout.HelpBox(
                    "No UXML file active.\nCreate a new page or open an existing UXML file.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Active:", GUILayout.Width(50f));
                EditorGUILayout.LabelField(Path.GetFileName(_activeUxmlPath), EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();

                string dir = Path.GetDirectoryName(_activeUxmlPath);
                EditorGUILayout.LabelField(dir, EditorStyles.miniLabel);
            }
        }

        private void CreateNewPage()
        {
            string path = EditorUtility.SaveFilePanel(
                "Create New M3 Page",
                "Assets",
                "NewPage.uxml",
                "uxml");

            if (string.IsNullOrEmpty(path)) return;

            UxmlExporter.CreatePageScaffold(path);

            string relativePath = ToRelativePath(path);
            if (relativePath != null)
            {
                AssetDatabase.Refresh();
                _activeUxmlPath = relativePath;
                OpenInUIBuilder(relativePath);
            }
        }

        private void OpenExistingPage()
        {
            string path = EditorUtility.OpenFilePanel(
                "Open UXML Page",
                "Assets",
                "uxml");

            if (string.IsNullOrEmpty(path)) return;

            string relativePath = ToRelativePath(path);
            if (relativePath != null)
            {
                _activeUxmlPath = relativePath;
                OpenInUIBuilder(relativePath);
            }
        }

        private void OnComponentSelected(ComponentRegistry.ComponentInfo info)
        {
            if (string.IsNullOrEmpty(_activeUxmlPath))
            {
                if (EditorUtility.DisplayDialog("Page Builder",
                    "No active UXML file. Create a new page first?", "Create", "Cancel"))
                {
                    CreateNewPage();
                    if (string.IsNullOrEmpty(_activeUxmlPath)) return;
                }
                else return;
            }

            string fullPath = Path.GetFullPath(_activeUxmlPath);
            if (!File.Exists(fullPath))
            {
                Debug.LogError($"[PageBuilder] UXML file not found: {_activeUxmlPath}");
                _activeUxmlPath = null;
                Repaint();
                return;
            }

            // Force UI Builder to save unsaved changes before we modify the file
            SaveAllOpenUIBuilders();

            UxmlExporter.AddComponentToUxml(fullPath, info);
            AssetDatabase.Refresh();

            // Re-open in UI Builder to refresh
            OpenInUIBuilder(_activeUxmlPath);
        }

        private static void OpenInUIBuilder(string relativePath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(relativePath);
            if (asset != null)
                AssetDatabase.OpenAsset(asset);
        }

        private void DetectActiveUxml()
        {
            // Check if current selection is a UXML file
            if (Selection.activeObject != null)
            {
                string path = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!string.IsNullOrEmpty(path) && path.EndsWith(".uxml"))
                {
                    _activeUxmlPath = path;
                    return;
                }
            }
        }

        private void ClearPage()
        {
            if (string.IsNullOrEmpty(_activeUxmlPath)) return;

            if (!EditorUtility.DisplayDialog("Clear Page",
                "Remove all components from the page?", "Clear", "Cancel"))
                return;

            SaveAllOpenUIBuilders();

            string fullPath = Path.GetFullPath(_activeUxmlPath);
            if (!File.Exists(fullPath)) return;

            UxmlExporter.ClearPage(fullPath);
            AssetDatabase.Refresh();
            OpenInUIBuilder(_activeUxmlPath);
        }

        /// <summary>
        /// Forces any open UI Builder windows to save their current document,
        /// so disk reflects the latest state before we modify the UXML file.
        /// </summary>
        private static void SaveAllOpenUIBuilders()
        {
            // UI Builder's internal type — save via reflection
            var builderType = System.Type.GetType(
                "Unity.UI.Builder.Builder, UnityEditor.UIBuilderModule") ??
                System.Type.GetType(
                "Unity.UI.Builder.Builder, Unity.UI.Builder.Editor");

            if (builderType == null) return;

            var windows = Resources.FindObjectsOfTypeAll(builderType);
            foreach (var window in windows)
            {
                // Try SaveChanges() — Unity 2021+
                var saveMethod = builderType.GetMethod("SaveChanges",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (saveMethod != null)
                {
                    try { saveMethod.Invoke(window, null); }
                    catch { /* UI Builder may not have unsaved changes */ }
                }
            }

            // Fallback: save all assets
            AssetDatabase.SaveAssets();
        }

        private static string ToRelativePath(string absolutePath)
        {
            if (absolutePath.StartsWith(Application.dataPath))
                return "Assets" + absolutePath.Substring(Application.dataPath.Length);

            Debug.LogWarning("[PageBuilder] File must be inside the Assets folder.");
            return null;
        }
    }
}
