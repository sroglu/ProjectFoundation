using System.Collections.Generic;
using PFound.HubApp.Save;
using UnityEngine;

namespace PFound.HubApp.Services.PhotoAlbum
{
	/// <summary>
	/// Cross-game shared photo album persisted under <c>SaveSchema.Shared[profileId].Photos</c>.
	/// JPEG files live at <c>{photosRoot}/{profileId}/{photoId}.jpg</c>; metadata stays in save.json.
	/// </summary>
	/// <remarks>
	/// Capacity contract (HubApp_TDD §5.4.3): cap of 200 photos per profile. The service
	/// performs FIFO eviction (oldest entry first) silently — the parent-gated "delete oldest
	/// to make room?" UX described in the TDD lives in the Shell, not in this service. Storage
	/// is finite; if we don't auto-evict, capture would fail mid-game which is a worse UX than
	/// silent rotation.
	/// Caller is responsible for sizing the input texture before capture
	/// (longest-edge ≤ 1920 px target) — this service writes the texture as-is and records its
	/// actual dimensions in the entry. Sizing is a separate concern (different per mini-game's
	/// rendering pipeline) and would couple this service to RenderTexture lifecycles otherwise.
	/// </remarks>
	public interface IPhotoAlbumService
	{
		/// <summary>Maximum photos retained per profile. Older entries are FIFO-evicted (file deleted) when exceeded.</summary>
		int MaxPhotosPerProfile { get; }

		/// <summary>
		/// Encodes <paramref name="snapshot"/> as JPEG, writes it to the photos directory, appends
		/// a <see cref="PhotoEntry"/>, evicts oldest entries past <see cref="MaxPhotosPerProfile"/>,
		/// and queues <see cref="MiniGame.Signals.PhotoCapturedSignal"/>. Returns the new entry.
		/// </summary>
		/// <remarks>
		/// <para><b>Texture ownership:</b> the caller OWNS <paramref name="snapshot"/>. This service
		/// reads pixel data (<c>EncodeToJPG</c>) and records width/height but does NOT retain a
		/// reference or destroy the texture. After <see cref="Capture"/> returns, the caller MUST
		/// <c>UnityEngine.Object.Destroy(snapshot)</c> (or <c>DestroyImmediate</c> in EditMode) —
		/// every undestroyed capture leaks one Texture2D per photo.</para>
		/// </remarks>
		PhotoEntry Capture(string profileId, Texture2D snapshot, string sourceGame, string sceneTag);

		IReadOnlyList<PhotoEntry> GetPhotosFor(string profileId);

		/// <summary>Returns the absolute file path to the JPEG for <paramref name="entry"/>.</summary>
		string GetPhotoPath(string profileId, PhotoEntry entry);
	}
}
