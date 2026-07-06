using System.Collections.Generic;
using PFound.Utilities.EditorHelpers;
using PFound.UserPrefs;

namespace PFound.UserPrefs.Editor
{
    internal sealed class UserPrefsAssetProvider : IGameSpecificAssetProvider
    {
        public IEnumerable<GameSpecificAssetRegistration> GetRegistrations()
        {
            yield return GameSpecificAssetRegistration.For<UserPrefsSettings>(
                "Assets/GameSpecific/UserPrefs/UserPrefsSettings.asset");
        }
    }
}
