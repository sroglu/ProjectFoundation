using System.IO;
using UnityEditor;
using UnityEngine;

namespace PFound.UserPrefs.Editor
{
    internal static class PrefsMenuItems
    {
        [MenuItem("Tools/PFound/UserPrefs/Open Inspector")]
        private static void OpenInspector() => PrefsInspectorWindow.ShowWindow();

        [MenuItem("Tools/PFound/UserPrefs/Reveal JSON File")]
        private static void RevealJsonFile()
        {
            var path = Path.Combine(Application.persistentDataPath, "userprefs.json");
            EditorUtility.RevealInFinder(path);
        }

        [MenuItem("Tools/PFound/UserPrefs/Clear All Stored Data")]
        private static void ClearAll()
        {
            if (!EditorUtility.DisplayDialog("Clear All Stored Data",
                    "This deletes the JSON file under Application.persistentDataPath AND clears ALL PlayerPrefs " +
                    "for this project. UserPrefs has no per-key namespace on PlayerPrefs, so other code that uses " +
                    "PlayerPrefs directly will be wiped too. Continue?",
                    "Clear", "Cancel"))
                return;

            var jsonPath = Path.Combine(Application.persistentDataPath, "userprefs.json");
            if (File.Exists(jsonPath)) File.Delete(jsonPath);
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("[UserPrefs] Cleared JSON file and PlayerPrefs.");
        }
    }
}
