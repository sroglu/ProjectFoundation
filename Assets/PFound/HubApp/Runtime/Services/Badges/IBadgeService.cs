using System.Collections.Generic;
using PFound.HubApp.Save;

namespace PFound.HubApp.Services.Badges
{
	/// <summary>
	/// Cross-game badge ledger persisted under <c>SaveSchema.Shared[profileId].Badges</c>.
	/// </summary>
	/// <remarks>
	/// Mini-games award badges by calling <see cref="Award"/> directly on the injected service —
	/// the framework's <c>SignalTracker</c> carries no payload, so the TDD's "publish a
	/// BadgeEarnedEvent with id" pattern is implemented as a direct interface call instead.
	/// Decoupling is preserved because mini-games depend on the interface, not the concrete service.
	/// On a *new* earn, <see cref="MiniGame.Signals.BadgePersistedSignal"/> is queued; UI listeners
	/// then query <see cref="GetBadgesFor"/> for the latest list.
	/// Idempotent: awarding the same badge twice for the same profile is a silent no-op (no signal).
	/// </remarks>
	public interface IBadgeService
	{
		/// <summary>
		/// Records <paramref name="badgeId"/> against <paramref name="profileId"/>. Returns true if
		/// this was a new earn (signal queued); false if the profile already owned the badge.
		/// <paramref name="sourceGame"/> tags the originating mini-game for filtering in the gallery
		/// — pass <c>null</c> if awarding from a non-mini-game context (e.g., admin/debug tools).
		/// </summary>
		bool Award(string profileId, string badgeId, string sourceGame);

		bool HasBadge(string profileId, string badgeId);

		/// <summary>Snapshot of all badges earned by this profile in earn order. Empty list if profile has none.</summary>
		IReadOnlyList<EarnedBadge> GetBadgesFor(string profileId);
	}
}
