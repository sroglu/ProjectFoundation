using System.IO;
using UnityEditor;
using UnityEngine;

namespace PFound.UserPrefs.Editor
{
    public sealed class PrefsInspectorWindow : EditorWindow
    {
        private const string MenuPath = "Window/PFound/UserPrefs Inspector";

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            var w = GetWindow<PrefsInspectorWindow>("UserPrefs");
            w.minSize = new Vector2(360, 240);
            w.Show();
        }

        private Vector2 _scroll;

        private void OnGUI()
        {
            EditorGUILayout.LabelField("UserPrefs Inspector", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Lists keys defined in the active IPrefsStore. UserPrefs does not expose a global accessor, " +
                "so resolution depends on your DI setup. This window is informational; use the menu items below " +
                "for direct PlayerPrefs / JSON file actions.",
                MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Storage Files", EditorStyles.boldLabel);

            var jsonPath = Path.Combine(Application.persistentDataPath, "userprefs.json");
            EditorGUILayout.LabelField("JSON file:", jsonPath);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reveal JSON file"))
                    EditorUtility.RevealInFinder(jsonPath);

                bool exists = File.Exists(jsonPath);
                using (new EditorGUI.DisabledScope(!exists))
                {
                    if (GUILayout.Button("Delete JSON file"))
                    {
                        if (EditorUtility.DisplayDialog("Delete JSON",
                                $"Delete '{jsonPath}'?", "Delete", "Cancel"))
                        {
                            File.Delete(jsonPath);
                            Repaint();
                        }
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("PlayerPrefs", EditorStyles.boldLabel);
            if (GUILayout.Button("Clear ALL PlayerPrefs (entire project!)"))
            {
                if (EditorUtility.DisplayDialog("Clear PlayerPrefs",
                        "This clears ALL PlayerPrefs for this project, not just UserPrefs keys. Continue?",
                        "Clear", "Cancel"))
                {
                    PlayerPrefs.DeleteAll();
                    PlayerPrefs.Save();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Settings asset", EditorStyles.boldLabel);
            var so = AssetDatabase.LoadAssetAtPath<UserPrefsSettings>(
                "Assets/GameSpecific/UserPrefs/UserPrefsSettings.asset");
            if (so == null)
            {
                EditorGUILayout.HelpBox(
                    "UserPrefsSettings asset not yet created (check 'Assets/GameSpecific/UserPrefs/'). " +
                    "GameSpecificAssetGuard creates it automatically every 5 seconds.",
                    MessageType.Warning);
            }
            else
            {
                if (GUILayout.Button("Select Settings asset"))
                    Selection.activeObject = so;
            }
        }
    }
}
