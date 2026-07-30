using System.Collections.Generic;
using PFound.TweenPresetLibrary;
using PFound.TweenPresetLibrary.Core;
using PFound.Utilities.EditorHelpers;

namespace PFound.SampleGame.Editor
{
    /// <summary>TweenPresetLibrary: a single-channel fade-in preset (0 → 1 over 0.3s, OutQuad).</summary>
    internal sealed class TweenPresetProvider : IGameSpecificAssetProvider
    {
        private const string Root = SampleAssetSetup.GameSpecificRoot + "/TweenPresetLibrary";

        public IEnumerable<GameSpecificAssetRegistration> GetRegistrations()
        {
            yield return GameSpecificAssetRegistration.For<TweenPreset>(
                Root + "/SampleFadePreset.asset", factory: Configure);
        }

        private static void Configure(TweenPreset preset)
        {
            SampleAssetSetup.EnsureFolder(Root);
            // Public channel fields — direct assignment. Only Fade carries playable time; the other
            // channels stay at their zero defaults (TweenPreset resets non-playable ones on deserialize).
            preset.Fade.Duration = 0.3f;
            preset.Fade.Ease = Ease.OutQuad;
            preset.Fade.EndValue = 1f;
            preset.Fade.ForceInitialValue = true;
            preset.Fade.ForcedInitialValue = 0f;
        }
    }
}
