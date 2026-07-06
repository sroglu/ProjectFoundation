using UnityEngine;

namespace PFound.HubApp.MiniGame
{
    /// <summary>
    /// Per-host-scene reference holder. Lives on the active scene's
    /// <c>MiniGameContentRoot</c> and exposes scene-level objects (camera, …) that a
    /// loaded <see cref="IMiniGameModule"/> needs but cannot serialize itself — a module
    /// prefab is instantiated at runtime and Unity forbids a prefab asset from holding a
    /// cross-scene reference. The host reads this at load time and threads it into
    /// <see cref="MiniGameContext.Scene"/>, so every mini-game reaches these refs through
    /// the same context channel instead of scanning the scene (<c>Find</c> / <c>Camera.main</c>).
    ///
    /// Optional: host scenes that expose nothing simply omit the component, and modules
    /// that need no scene refs ignore <see cref="MiniGameContext.Scene"/>.
    /// </summary>
    public sealed class MiniGameSceneContext : MonoBehaviour
    {
        [SerializeField] Camera _camera;

        /// <summary>The scene camera that renders the loaded mini-game module.</summary>
        public Camera Camera => _camera;
    }
}
