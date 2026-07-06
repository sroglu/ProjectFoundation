using System.Collections.Generic;
using UnityEngine;

namespace PFound.HubApp.MiniGame
{
	/// <summary>
	/// Authored metadata for a mini-game tile on the Home screen. Each Playnest mini-game ships
	/// one of these as a ScriptableObject under <c>Assets/GameSpecific/MiniGames/{gameId}/</c>.
	/// </summary>
	/// <remarks>
	/// <see cref="GameId"/> MUST match the <see cref="IMiniGameModule.GameId"/> of the runtime
	/// module — the host uses it to scope the save and tag analytics events. Mismatch is a hard
	/// authoring bug; runtime check is in <see cref="MiniGameHost.Load"/>.
	/// <see cref="DisplayNameKey"/> / <see cref="IconKey"/> are content keys, not literal strings —
	/// the Shell renders them via <c>LocalizationService</c> so EN/TR switching works without
	/// re-authoring SOs.
	/// </remarks>
	[CreateAssetMenu(menuName = "Playnest/HubApp/Mini-Game Definition", fileName = "NewMiniGameDefinition")]
	public class MiniGameDefinition : ScriptableObject
	{
		[Tooltip("Stable lower_snake_case id, must match IMiniGameModule.GameId. Used for save scoping + analytics.")]
		public string GameId;

		[Tooltip("Localization key for the tile label, e.g. 'minigame.tossytoss.name'.")]
		public string DisplayNameKey;

		[Tooltip("Localization (or asset) key for the tile icon. Resolved by Shell at render time.")]
		public string IconKey;

		[Tooltip("OS-level app display name used ONLY for a single-game (DirectBoot) standalone build, " +
		         "e.g. 'Sumo Bumo' / 'Camping Adventure'. Deliberately a literal, NOT a Localization key: " +
		         "the launcher/store label is a build-manifest string set per build, not in-game UI text. " +
		         "Ignored by the full hub build (which keeps the project's 'Playnest' product name).")]
		public string BuildProductName;

		[Tooltip("AssetSystem address of the mini-game's root prefab — the prefab MUST contain exactly one IMiniGameModule MonoBehaviour. PrefabMiniGameLoader resolves this address at Load() time.")]
		public string ModuleAddress;

		[Tooltip("If true, the tile is playable now and ModuleAddress must resolve to a valid prefab in AssetSystem (mismatch = hard error). If false, the tile shows 'coming soon' — used both for not-yet-built mini-games (early development) and for premium content awaiting IAP.")]
		public bool Unlocked = true;

		[Tooltip("Profile age bands this mini-game supports (e.g. '2-3', '3-4', '4+'). Used by the Home screen to filter tiles by the active profile's age band. Empty list = visible to all profiles (backward-compatible default for legacy SOs).")]
		public List<string> SupportedAgeBands = new();

		/// <summary>
		/// Returns true if the tile should be visible for a profile with the given age band.
		/// Empty <see cref="SupportedAgeBands"/> is treated as "all bands" so existing SOs
		/// authored before the field was added remain visible.
		/// </summary>
		public bool IsVisibleFor(string profileAgeBand)
		{
			if (SupportedAgeBands == null || SupportedAgeBands.Count == 0) return true;
			if (string.IsNullOrEmpty(profileAgeBand)) return true;
			return SupportedAgeBands.Contains(profileAgeBand);
		}
	}
}
