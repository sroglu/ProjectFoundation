namespace PFound.StartupOrchestration
{
    /// <summary>
    /// Categorized, localizable label for "what the app is currently waiting on" during boot.
    /// Drives the LoadingScreen status text via the localization key <c>loading.{enum-name}</c>
    /// (PascalCase verbatim). A missing key shows the raw enum name so the gap is visible in dev.
    /// Adding a new value requires a new localization row.
    /// </summary>
    public enum WaitReason
    {
        // 0 reserved for invalid / unset.

        // ----- Preload-phase reasons (1-9) -----
        Catalog       = 1,   // AssetSystem catalog download
        Translations  = 2,   // Localization sheet fetch
        Save          = 3,   // Save schema initial read (rare — usually Tier 0)
        RemoteConfig  = 4,   // RemoteConfig pull
        BundlesVerify = 5,   // SHA verify of cached bundles

        // ----- Load + post-load reasons (10+) -----
        Systems       = 10,  // generic Load phase — "loading game systems…"
        ShaderWarmup  = 11,  // future — first-game-tap warmup
        Scene         = 12,  // future — async SceneManager.LoadSceneAsync
    }
}
