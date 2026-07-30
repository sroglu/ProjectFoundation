using System.Collections.Generic;
using PFound.ContentDelivery.Editor;
using PFound.Utilities.EditorHelpers;

namespace PFound.SampleGame.Editor
{
    /// <summary>ContentDelivery: a minimal, valid <see cref="AssetGroup"/> reference (no authored entries).</summary>
    internal sealed class SampleAssetGroupProvider : IGameSpecificAssetProvider
    {
        private const string Root = SampleAssetSetup.GameSpecificRoot + "/ContentDelivery";

        public IEnumerable<GameSpecificAssetRegistration> GetRegistrations()
        {
            yield return GameSpecificAssetRegistration.For<AssetGroup>(
                Root + "/SampleAssetGroup.asset", factory: Configure);
        }

        private static void Configure(AssetGroup group)
        {
            SampleAssetSetup.EnsureFolder(Root);
            group.BundleName = "sample";
            group.Distribution = DistributionMode.Local;      // sample ships in-build; no CDN dependency
            group.Packing = BundlePackingMode.PackTogether;
            // Phase left at its Standard default (avoids a runtime PFound.ContentDelivery reference).
            // Entries deliberately empty — a valid reference group without inventing a fake asset ref.
        }
    }
}
