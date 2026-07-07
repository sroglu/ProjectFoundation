using System.Collections.Generic;
using PFound.GuidedOnboardingFlow.Authoring;
using PFound.GuidedOnboardingFlow.Core;
using PFound.Utilities.EditorHelpers;
using UnityEditor;
using UnityEngine;

namespace PFound.SampleGame.Editor
{
    /// <summary>GuidedOnboardingFlow: a one-tutorial catalog whose single step logs a welcome line.</summary>
    internal sealed class TutorialCatalogProvider : IGameSpecificAssetProvider
    {
        private const string Root = SampleAssetSetup.GameSpecificRoot + "/GuidedOnboardingFlow";

        public IEnumerable<GameSpecificAssetRegistration> GetRegistrations()
        {
            yield return GameSpecificAssetRegistration.For<TutorialCatalog>(
                Root + "/TutorialCatalog.asset", factory: Configure);
        }

        private static void Configure(TutorialCatalog catalog)
        {
            SampleAssetSetup.EnsureFolder(Root);

            // The guard has already persisted the catalog, so a step sub-asset can be attached here.
            var step = ScriptableObject.CreateInstance<LogStepAuthoring>();
            step.name = "WelcomeLogStep";
            SampleAssetSetup.SetField(step, "_message", "Welcome to the PFound sample game.");
            AssetDatabase.AddObjectToAsset(step, catalog);

            var definition = new TutorialDefinition();
            SampleAssetSetup.SetField(definition, "_id", new TutorialId(1));
            SampleAssetSetup.SetField(definition, "_displayName", "Sample Onboarding");
            SampleAssetSetup.SetField(definition, "_steps", new List<TutorialStepAuthoring> { step });

            SampleAssetSetup.SetField(catalog, "_definitions", new List<TutorialDefinition> { definition });
        }
    }
}
