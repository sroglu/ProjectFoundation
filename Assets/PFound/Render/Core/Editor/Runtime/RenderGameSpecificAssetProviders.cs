using UnityEditor;

namespace PFound.Render.Core.Editor
{
    /// <summary>
    /// Extension point for registering <c>IGameSpecificAssetProvider</c> instances
    /// owned by future Render phases (e.g., Phase 2 ColorGrading default LUT,
    /// Phase 6 PostProcess default profile). Providers registered here have their
    /// generated assets auto-created under <c>Assets/GameSpecific/Render/</c> by the
    /// parent project's <c>GameSpecificAssetGuard</c>.
    /// </summary>
    /// <remarks>
    /// Phase 1 ships this placeholder so the wiring convention is documented and
    /// future phases land predictably. No providers are registered in Phase 1 —
    /// the module produces no runtime assets.
    /// </remarks>
    internal static class RenderGameSpecificAssetProviders
    {
        // Phase 1: intentionally empty.
        // Future phases will add fields/methods like:
        //   [InitializeOnLoadMethod]
        //   private static void RegisterColorGradingDefaultLut() { /* ... */ }
    }
}
