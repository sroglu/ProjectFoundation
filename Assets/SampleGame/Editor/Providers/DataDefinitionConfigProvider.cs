using System.Collections.Generic;
using PFound.GameDataStore.Entries;
using PFound.Utilities.EditorHelpers;

namespace PFound.SampleGame.Editor
{
    /// <summary>GameDataStore: a small building/item identifier table (NumericId + TextId pairs).</summary>
    internal sealed class DataDefinitionConfigProvider : IGameSpecificAssetProvider
    {
        private const string Root = SampleAssetSetup.GameSpecificRoot + "/GameDataStore";

        public IEnumerable<GameSpecificAssetRegistration> GetRegistrations()
        {
            yield return GameSpecificAssetRegistration.For<DataDefinitionConfig>(
                Root + "/DataDefinitionConfig.asset", factory: Configure);
        }

        private static void Configure(DataDefinitionConfig config)
        {
            SampleAssetSetup.EnsureFolder(Root);

            SampleAssetSetup.SetField(config, "_buildingDefinitions", new List<DataIdentifierDefinition>
            {
                new DataIdentifierDefinition { NumericId = 1, TextId = "sample.building.house" },
                new DataIdentifierDefinition { NumericId = 2, TextId = "sample.building.farm" },
            });
            SampleAssetSetup.SetField(config, "_itemDefinitions", new List<DataIdentifierDefinition>
            {
                new DataIdentifierDefinition { NumericId = 101, TextId = "sample.item.wood" },
                new DataIdentifierDefinition { NumericId = 102, TextId = "sample.item.stone" },
            });
        }
    }
}
