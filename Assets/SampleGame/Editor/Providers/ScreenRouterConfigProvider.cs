using System.Collections.Generic;
using PFound.ScreenRouter;
using PFound.Utilities.EditorHelpers;

namespace PFound.SampleGame.Editor
{
    /// <summary>ScreenRouter: an empty router config with blur disabled (needs no blur prefab).</summary>
    internal sealed class ScreenRouterConfigProvider : IGameSpecificAssetProvider
    {
        private const string Root = SampleAssetSetup.GameSpecificRoot + "/ScreenRouter";

        public IEnumerable<GameSpecificAssetRegistration> GetRegistrations()
        {
            yield return GameSpecificAssetRegistration.For<ScreenRouterConfig>(
                Root + "/ScreenRouterConfig.asset", factory: Configure);
        }

        private static void Configure(ScreenRouterConfig config)
        {
            SampleAssetSetup.EnsureFolder(Root);
            // _screens / _frames stay empty (default). Sorting/pooling/queue defaults are fine as-is.
            // Blur off so the config asset validates without a blur prefab wired.
            SampleAssetSetup.SetField(config, "_useBackgroundBlur", false);
        }
    }
}
