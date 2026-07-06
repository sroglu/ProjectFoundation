using PFound.Signaling;

namespace PFound.HubApp.MiniGame.Signals
{
	/// <summary>
	/// Queued by <see cref="MiniGameHost.Pause"/>. Mirrors Unity's <c>OnApplicationPause</c> /
	/// parent-gate-open semantics — the running mini-game has been suspended.
	/// </summary>
	public class MiniGamePausedSignal : SignalBase { }
}
