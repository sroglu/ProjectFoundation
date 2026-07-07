namespace PFound.LocalizationService.Unity
{
    /// <summary>Persists the player's chosen language code across sessions.</summary>
    public interface ILanguagePreferenceStore
    {
        bool TryGet(out string code);
        void Set(string code);
        void Clear();
    }
}
