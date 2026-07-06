using PFound.Signaling;

namespace PFound.HubApp.MiniGame.Signals
{
	/// <summary>
	/// Fired by <see cref="HubApp.Services.Badges.IBadgeService"/> the first time a badge is awarded
	/// for the active profile (idempotent — no signal on duplicate award). UI subscribes to animate
	/// a toast and then queries <c>BadgeService.GetBadgesFor(profileId)</c> for the latest list.
	/// </summary>
	public class BadgePersistedSignal : SignalBase { }
}
