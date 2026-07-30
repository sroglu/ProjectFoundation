using UnityEngine;

namespace PFound.UserPrefs
{
    [CreateAssetMenu(menuName = "GameSpecific/UserPrefsSettings", fileName = "UserPrefsSettings")]
    public sealed class UserPrefsSettings : ScriptableObject
    {
        [Tooltip("Verbose logging for load/save/migration operations.")]
        public bool VerboseLogging = false;

        [Tooltip("In-editor 'Reset All' also clears PlayerPrefs (otherwise only JSON is cleared).")]
        public bool EditorResetClearsPlayerPrefs = true;

        [Tooltip("JSON file name (default: userprefs.json). Stored under Application.persistentDataPath.")]
        public string JsonFileName = "userprefs.json";
    }
}
