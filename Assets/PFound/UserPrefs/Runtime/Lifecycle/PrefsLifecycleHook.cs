using UnityEngine;

namespace PFound.UserPrefs
{
    internal sealed class PrefsLifecycleHook : MonoBehaviour
    {
        private const string GameObjectName = "[UserPrefsLifecycle]";
        private IPrefsStore _store;

        public static PrefsLifecycleHook Spawn(IPrefsStore store, IPrefsLogger logger)
        {
            var existing = GameObject.Find(GameObjectName);
            if (existing != null)
            {
                logger?.Info("Lifecycle hook GameObject already exists; destroying old instance.");
                Object.Destroy(existing);
            }

            var go = new GameObject(GameObjectName);
            go.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(go);
            var hook = go.AddComponent<PrefsLifecycleHook>();
            hook._store = store;
            return hook;
        }

        public void Detach()
        {
            _store = null;
            if (gameObject != null)
                Object.Destroy(gameObject);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) _store?.Flush();
        }

        private void OnApplicationQuit()
        {
            _store?.Flush();
        }
    }
}
