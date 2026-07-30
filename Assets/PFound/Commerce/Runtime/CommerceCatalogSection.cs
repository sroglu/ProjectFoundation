using PFound.RemoteGameConfig.Core;

namespace PFound.Commerce
{
    /// <summary>
    /// The RemoteGameConfig section the product catalog is served in. The catalog is a small JSON tuning
    /// table (product → cost / rewards / category / per-platform SKUs) carried as a single string value,
    /// so LiveOps can add, price, and bundle products without an app update. A monetization flag
    /// ("adsEnabled") rides alongside it. Commerce reads this through the config service; it never owns a
    /// catalog fetch of its own.
    /// </summary>
    public sealed class CommerceCatalogSection : GameConfigSection
    {
        public override string SectionName => "commerce";

        public override void WriteDefaults(SectionDefaultWriter defaults)
        {
            // Empty embedded catalog by default: the game ships its baked catalog via a config manifest
            // or the first remote fetch. An empty object parses to a zero-product catalog (no crash).
            defaults.Set("catalogJson", "{ \"products\": {} }");
            defaults.Set("adsEnabled", true);
        }

        /// <summary>The raw catalog JSON to parse into a <see cref="Core.ProductCatalog"/>.</summary>
        public string CatalogJson => GetString("catalogJson", "{ \"products\": {} }");

        /// <summary>Global monetization switch — false forces the no-ads path on for everyone.</summary>
        public bool AdsEnabled => GetBool("adsEnabled", true);
    }
}
