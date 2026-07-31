namespace PFound.ServerOperation.Core
{
    /// <summary>
    /// Localized user-facing text looked up by key. The game wraps its own localization service behind this,
    /// so the Core resolves a failure message without referencing any localization type. Returns true with the
    /// text when the key exists, false when it does not (letting the presenter fall back).
    /// </summary>
    public interface IUserMessageSource
    {
        bool TryGet(string key, out string message);
    }
}
