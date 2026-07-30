using System.Collections.Generic;

namespace PFound.TweenPresetLibrary
{
    /// <summary>
    /// Resolves authored <see cref="TweenPreset"/>s by string key. Deliberately decoupled from how the presets were
    /// obtained: populate it from a set fetched through the content-delivery AssetSystem (each preset is a
    /// ScriptableObject, addressable via <c>AssetManager.LoadAssetAsync&lt;TweenPreset&gt;(address)</c> — no module
    /// code needed), or from a static authored list injected at startup. The one source this module does NOT use is
    /// <c>Resources.Load</c> (baked-in, not post-launch-updatable).
    /// </summary>
    public interface ITweenPresetProvider
    {
        /// <summary>True + the preset when <paramref name="key"/> is known; false + null otherwise.</summary>
        bool TryGet(string key, out TweenPreset preset);

        /// <summary>Whether a preset is registered under <paramref name="key"/>.</summary>
        bool Contains(string key);

        /// <summary>Every registered key.</summary>
        IReadOnlyCollection<string> Keys { get; }
    }
}
