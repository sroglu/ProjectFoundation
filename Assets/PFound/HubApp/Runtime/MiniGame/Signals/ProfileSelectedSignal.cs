using PFound.Signaling;

namespace PFound.HubApp.MiniGame.Signals
{
	/// <summary>
	/// Notifies UI that <see cref="HubApp.Services.Profile.IProfileService.ActiveProfileId"/> changed
	/// (created, switched, or cleared). Listeners must query the service for the new id —
	/// framework signals carry no payload (see <c>PFound.Signaling</c> MODULE.md).
	/// </summary>
	public class ProfileSelectedSignal : SignalBase { }
}
