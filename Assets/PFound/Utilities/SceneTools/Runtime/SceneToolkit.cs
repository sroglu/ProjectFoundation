using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PFound.Utilities.SceneTools
{
    /// <summary>
    /// Helpers for inspecting and manipulating loaded scenes through the
    /// <see cref="SceneManager"/>. All queries operate on the set of scenes that
    /// are currently present in the runtime scene list.
    /// </summary>
    public static class SceneToolkit
    {
        /// <summary>Name used for the transient probe object that reveals the DontDestroyOnLoad scene.</summary>
        private const string DontDestroyProbeName = "PFound.SceneToolkit.DontDestroyProbe";

        /// <summary>
        /// Returns every scene that is currently loaded, in scene-list order.
        /// </summary>
        public static List<Scene> GetLoadedScenes()
        {
            int count = SceneManager.sceneCount;
            var result = new List<Scene>(count);
            for (int i = 0; i < count; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                {
                    result.Add(scene);
                }
            }
            return result;
        }

        /// <summary>
        /// Returns every loaded scene that satisfies <paramref name="match"/>.
        /// </summary>
        public static List<Scene> GetLoadedScenes(Predicate<Scene> match)
        {
            int count = SceneManager.sceneCount;
            var result = new List<Scene>();
            for (int i = 0; i < count; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && match(scene))
                {
                    result.Add(scene);
                }
            }
            return result;
        }

        /// <summary>
        /// True when <paramref name="scene"/> is the one flagged active by the
        /// <see cref="SceneManager"/>.
        /// </summary>
        public static bool IsSceneActive(Scene scene)
        {
            return SceneManager.GetActiveScene() == scene;
        }

        /// <summary>
        /// Root GameObjects belonging to a single scene.
        /// </summary>
        public static GameObject[] GetRootObjects(Scene scene)
        {
            return scene.GetRootGameObjects();
        }

        /// <summary>
        /// Root GameObjects gathered from every currently loaded scene.
        /// </summary>
        public static List<GameObject> GetAllRootObjects()
        {
            var roots = new List<GameObject>();
            foreach (Scene scene in GetLoadedScenes())
            {
                roots.AddRange(scene.GetRootGameObjects());
            }
            return roots;
        }

        /// <summary>
        /// Begins asynchronous unloading of every loaded scene and returns the
        /// resulting operations. Unity keeps at least one scene resident, so the
        /// last operation may resolve to <c>null</c> and is skipped.
        /// </summary>
        public static List<AsyncOperation> UnloadAllScenesAsync()
        {
            return UnloadScenesAsync(keep: default, hasKeep: false);
        }

        /// <summary>
        /// Begins asynchronous unloading of every loaded scene except
        /// <paramref name="keep"/>.
        /// </summary>
        public static List<AsyncOperation> UnloadAllScenesExcept(Scene keep)
        {
            return UnloadScenesAsync(keep, hasKeep: true);
        }

        private static List<AsyncOperation> UnloadScenesAsync(Scene keep, bool hasKeep)
        {
            var operations = new List<AsyncOperation>();
            foreach (Scene scene in GetLoadedScenes())
            {
                if (hasKeep && scene == keep)
                {
                    continue;
                }

                AsyncOperation operation = SceneManager.UnloadSceneAsync(scene);
                if (operation != null)
                {
                    operations.Add(operation);
                }
            }
            return operations;
        }

        /// <summary>
        /// Reloads every currently loaded scene, preserving the additive layout.
        /// </summary>
        public static List<AsyncOperation> ReloadAllLoadedScenes()
        {
            var operations = new List<AsyncOperation>();
            foreach (Scene scene in GetLoadedScenes())
            {
                operations.Add(ReloadScene(scene));
            }
            return operations;
        }

        /// <summary>
        /// Reloads a single scene. When it is the only resident scene a single
        /// (non-additive) load is used; otherwise the scene is unloaded and then
        /// loaded additively so the surrounding layout survives.
        /// </summary>
        public static AsyncOperation ReloadScene(Scene scene)
        {
            string path = scene.path;
            bool additive = SceneManager.sceneCount > 1;
            if (additive)
            {
                SceneManager.UnloadSceneAsync(scene);
                return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            }
            return SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
        }

        /// <summary>
        /// Resolves the special DontDestroyOnLoad scene by parking a transient
        /// probe object in it and reading the scene handle back.
        /// </summary>
        public static Scene GetDontDestroyOnLoadScene()
        {
            // The DontDestroyOnLoad scene only exists at runtime, and DontDestroyOnLoad itself
            // throws outside play mode — there is nothing to probe in the editor.
            if (!Application.isPlaying)
            {
                return default;
            }

            var probe = new GameObject(DontDestroyProbeName);
            UnityEngine.Object.DontDestroyOnLoad(probe);
            Scene scene = probe.scene;
            DestroyProbe(probe);
            return scene;
        }

        /// <summary>
        /// Root GameObjects that live under DontDestroyOnLoad, excluding the
        /// transient probe used to reach the scene.
        /// </summary>
        public static List<GameObject> GetDontDestroyOnLoadRoots()
        {
            // No DontDestroyOnLoad scene exists outside play mode; creating a probe here would
            // throw (DontDestroyOnLoad is play-mode only) and leak the object. Return empty.
            if (!Application.isPlaying)
            {
                return new List<GameObject>();
            }

            var probe = new GameObject(DontDestroyProbeName);
            UnityEngine.Object.DontDestroyOnLoad(probe);
            Scene scene = probe.scene;

            var roots = new List<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root != probe)
                {
                    roots.Add(root);
                }
            }

            DestroyProbe(probe);
            return roots;
        }

        private static void DestroyProbe(GameObject probe)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(probe);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }
    }
}
