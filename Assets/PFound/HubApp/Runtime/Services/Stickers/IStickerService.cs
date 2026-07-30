using System.Collections.Generic;
using PFound.HubApp.Save;

namespace PFound.HubApp.Services.Stickers
{
	/// <summary>
	/// Cross-game sticker ledger persisted under <c>SaveSchema.Shared[profileId].Stickers</c>.
	/// Mirrors <see cref="Badges.IBadgeService"/> idempotency contract — stickers differ from
	/// badges only in semantics (lighter rewards, no Bronze/Silver/Gold tier, no locked variant);
	/// the persistence + signaling shape is identical.
	/// </summary>
	public interface IStickerService
	{
		/// <summary>Returns true on first earn (queues <see cref="MiniGame.Signals.StickerPersistedSignal"/>); false on duplicate.</summary>
		bool Award(string profileId, string stickerId, string sourceGame);

		bool HasSticker(string profileId, string stickerId);

		IReadOnlyList<EarnedSticker> GetStickersFor(string profileId);
	}
}
