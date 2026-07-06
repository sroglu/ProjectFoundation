namespace PFound.HubApp.Entitlement
{
    /// <summary>
    /// Hub-level query for whether the current profile is entitled to a content pack
    /// (a unit of distributable game content — environment, character roster, theme,
    /// etc.). MVP implementations return <c>true</c> for the base pack of each shipped
    /// mini-game and <c>false</c> for premium / future packs that have not yet been
    /// purchased or downloaded.
    /// </summary>
    /// <remarks>
    /// Pack ids are dotted snake_case rooted in the owning mini-game's GameId. Examples:
    /// <list type="bullet">
    /// <item><c>camping.base</c> — Forest + Seaside ship in MVP</item>
    /// <item><c>camping.lake</c> — premium pack, post-launch</item>
    /// <item><c>tossy.base</c> — Earth/Moon/Mars MVP</item>
    /// <item><c>tossy.jupiter</c> — premium</item>
    /// </list>
    /// Mini-games query this service on lobby mount to decide which environments /
    /// characters / themes to surface. Unknown pack ids MUST return false (default
    /// deny) — additions to the catalog do not silently enable old builds.
    /// </remarks>
    public interface IContentPackEntitlement
    {
        /// <summary>
        /// True if the active profile is entitled to load assets from <paramref name="packId"/>.
        /// </summary>
        bool IsEntitled(string packId);
    }
}
