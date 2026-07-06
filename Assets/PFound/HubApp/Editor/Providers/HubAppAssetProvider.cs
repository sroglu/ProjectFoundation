using System.Collections.Generic;
using PFound.Utilities.EditorHelpers;

namespace PFound.HubApp.Editor.Providers
{
	/// <summary>
	/// Declares GameSpecific assets that HubApp needs at the project level.
	/// <see cref="GameSpecificAssetGuard"/> discovers this via reflection and auto-creates
	/// missing assets every 5 seconds.
	/// </summary>
	/// <remarks>
	/// <b>AudioMixer</b> is NOT registered here — <c>AudioMixer</c> is not a
	/// <c>ScriptableObject</c> and requires <c>AudioMixerController.CreateMixerControllerAtPath</c>.
	/// The mixer at <c>Assets/GameSpecific/Audio/PlaynestAudioMixer.mixer</c> is created
	/// via Editor script (see Faz 1.4 prereq). Future SO-based assets (ProductCatalog,
	/// BadgeDefinition catalogs, etc.) will be registered here as they land.
	/// </remarks>
	internal class HubAppAssetProvider : IGameSpecificAssetProvider
	{
		public IEnumerable<GameSpecificAssetRegistration> GetRegistrations()
		{
			// No SO-based assets to auto-create yet.
			// AudioMixer → created via Editor script (not a SO).
			// ProductCatalog → deferred until Unity IAP SDK lands.
			// BadgeDefinition / StickerDefinition catalogs → per-game, Faz 3+.
			yield break;
		}
	}
}
