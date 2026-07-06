using System.Collections.Generic;
using PFound.Utilities.EditorHelpers;
using PFound.UISystem.Data;
using PFound.UISystem.Enums;
using UnityEngine;

namespace PFound.UISystem.Editor
{
    internal class UISystemAssetProvider : IGameSpecificAssetProvider
    {
        private const string ResourcesRoot = "Assets/GameSpecific/UISystem/Resources/UISystem";

        public IEnumerable<GameSpecificAssetRegistration> GetRegistrations()
        {
            yield return GameSpecificAssetRegistration.For<ThemeData>(
                ResourcesRoot + "/DefaultLight.asset", "UISystem/DefaultLight",
                so => DefaultAssetsSetup.ConfigureTheme(so, isLight: true));

            yield return GameSpecificAssetRegistration.For<ThemeData>(
                ResourcesRoot + "/DefaultDark.asset", "UISystem/DefaultDark",
                so => DefaultAssetsSetup.ConfigureTheme(so, isLight: false));

            yield return GameSpecificAssetRegistration.For<TypographyConfig>(
                ResourcesRoot + "/DefaultTypography.asset", "UISystem/DefaultTypography",
                so => DefaultAssetsSetup.ConfigureTypography(so));
        }
    }
}
