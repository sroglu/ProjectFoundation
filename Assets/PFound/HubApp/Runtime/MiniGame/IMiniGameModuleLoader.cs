using System;
using UnityEngine;

namespace PFound.HubApp.MiniGame
{
	/// <summary>
	/// Loads a mini-game's root prefab from AssetSystem and resolves its
	/// <see cref="IMiniGameModule"/> component. Replaces the Faz 1 scene-based
	/// loader: every mini-game is now a single self-contained prefab (per-game
	/// scenes were forcing one extra Build Settings entry + scene asset per
	/// mini-game with no actual content of their own — the prefab IS the scene).
	/// Production impl: <see cref="PrefabMiniGameLoader"/> over the
	/// <c>AssetSystem</c> bundle pipeline; tests can stub with a direct-ref
	/// loader so EditMode runs don't depend on AssetSystem catalog state.
	/// </summary>
	public interface IMiniGameModuleLoader
	{
		/// <summary>
		/// Resolve and instantiate the mini-game prefab at <paramref name="moduleAddress"/>.
		/// The new instance is parented under <paramref name="parent"/> when non-null;
		/// when null the instance lands at scene root. Use a parent to keep the active
		/// scene's hierarchy tidy (e.g. spawn under a per-scene <c>MiniGameContentRoot</c>).
		/// </summary>
		void Load(string moduleAddress, Transform parent, Action<IMiniGameModule, GameObject> onLoaded);
		void Unload(GameObject moduleInstance);
	}
}
