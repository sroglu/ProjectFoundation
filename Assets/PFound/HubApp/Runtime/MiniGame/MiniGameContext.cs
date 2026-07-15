using System;
using PFound.HubApp.Services.Badges;
using PFound.HubApp.Services.PhotoAlbum;
using PFound.HubApp.Services.Profile;
using PFound.HubApp.Services.Stickers;

namespace PFound.HubApp.MiniGame
{
	/// <summary>
	/// Bundle of services + control hooks handed to <see cref="IMiniGameModule.Initialize"/>.
	/// Read-only struct — captures a snapshot of the host's wiring at scene-init time so the
	/// mini-game holds stable references for its lifetime.
	/// </summary>
	/// <remarks>
	/// <see cref="Save"/> is a <see cref="IScopedSaveService"/>, NOT the root <c>ISaveService</c> —
	/// every Set/Get is auto-namespaced to <c>games.{profileId}.{gameId}</c>, so mini-games can
	/// only see/modify their own slice (HubApp_TDD §7.3 isolation contract).
	/// <see cref="RequestExit"/> is the mini-game's only way to ask the host to tear it down —
	/// the host owns the actual unload, the module just signals "I'm done" (e.g. user tapped the
	/// back button). The host then drives <c>OnExit</c>, scene unload, and GC.
	/// Audio + Localization are not yet on the surface — they land when AudioService ships and
	/// the per-mini-game audio/localization wrapper is in place. Adding them later is additive
	/// (new fields don't break existing modules).
	/// <see cref="Scene"/> is the host scene's <see cref="MiniGameSceneContext"/> (camera + other
	/// scene-level refs). Optional — null when the active scene exposes no scene context; modules
	/// that pan/track the scene camera read it, the rest ignore it.
	/// </remarks>
	public readonly struct MiniGameContext
	{
		public readonly IScopedSaveService Save;
		public readonly IProfileService Profile;
		public readonly IBadgeService Badges;
		public readonly IStickerService Stickers;
		public readonly IPhotoAlbumService PhotoAlbum;
		public readonly Action RequestExit;
		public readonly MiniGameSceneContext Scene;

		public MiniGameContext(
			IScopedSaveService save,
			IProfileService profile,
			IBadgeService badges,
			IStickerService stickers,
			IPhotoAlbumService photoAlbum,
			Action requestExit,
			MiniGameSceneContext scene = null)
		{
			if (save == null) throw new ArgumentNullException(nameof(save));
			if (profile == null) throw new ArgumentNullException(nameof(profile));
			if (badges == null) throw new ArgumentNullException(nameof(badges));
			if (stickers == null) throw new ArgumentNullException(nameof(stickers));
			if (photoAlbum == null) throw new ArgumentNullException(nameof(photoAlbum));
			if (requestExit == null) throw new ArgumentNullException(nameof(requestExit));

			Save = save;
			Profile = profile;
			Badges = badges;
			Stickers = stickers;
			PhotoAlbum = photoAlbum;
			RequestExit = requestExit;
			Scene = scene;   // optional: host scene may expose no scene-level refs
		}
	}
}
