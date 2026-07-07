using System.Collections.Generic;
using PFound.HubApp.MiniGame;
using PFound.Utilities.EditorHelpers;

namespace PFound.SampleGame.Editor
{
    /// <summary>HubApp: a single unlocked mini-game tile pointing at the sample module address.</summary>
    internal sealed class MiniGameDefinitionProvider : IGameSpecificAssetProvider
    {
        private const string Root = SampleAssetSetup.GameSpecificRoot + "/HubApp";

        public IEnumerable<GameSpecificAssetRegistration> GetRegistrations()
        {
            yield return GameSpecificAssetRegistration.For<MiniGameDefinition>(
                Root + "/SampleMiniGame.asset", factory: Configure);
        }

        private static void Configure(MiniGameDefinition definition)
        {
            SampleAssetSetup.EnsureFolder(Root);
            definition.GameId = "sample";
            definition.DisplayNameKey = "minigame.sample.name";
            definition.IconKey = "minigame.sample.icon";
            definition.ModuleAddress = "minigames/sample";
            definition.Unlocked = true;
        }
    }
}
