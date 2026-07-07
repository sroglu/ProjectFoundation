using System.Collections.Generic;
using PFound.ContentDelivery.Editor;
using PFound.Utilities.EditorHelpers;

namespace PFound.SampleGame.Editor
{
    /// <summary>ContentDelivery: the editor build-config the sample game publishes its catalog under.</summary>
    internal sealed class CatalogEditorConfigProvider : IGameSpecificAssetProvider
    {
        private const string Root = SampleAssetSetup.GameSpecificRoot + "/ContentDelivery";

        public IEnumerable<GameSpecificAssetRegistration> GetRegistrations()
        {
            yield return GameSpecificAssetRegistration.For<CatalogEditorConfig>(
                Root + "/CatalogEditorConfig.asset", factory: Configure);
        }

        private static void Configure(CatalogEditorConfig config)
        {
            SampleAssetSetup.EnsureFolder(Root);
            config.GameId = "sample-game";
            config.ActiveEnvironment = "prod";
            config.Environments = new List<ContentEnvironmentEntry>
            {
                new ContentEnvironmentEntry { Name = "prod", BaseUrl = "https://cdn.example.com/sample-game/" }
            };
            // GroupsToBuild left empty: Scope defaults to AllGroups, so the sample group is picked up automatically.
        }
    }
}
