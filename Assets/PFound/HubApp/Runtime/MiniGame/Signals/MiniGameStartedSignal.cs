using PFound.Signaling;

namespace PFound.HubApp.MiniGame.Signals
{
	/// <summary>
	/// Queued by <see cref="MiniGameHost"/> immediately after <c>IMiniGameModule.Initialize</c> returns.
	/// Listeners (analytics, telemetry) query the host for the active definition; signal carries no payload.
	/// </summary>
	public class MiniGameStartedSignal : SignalBase { }
}
