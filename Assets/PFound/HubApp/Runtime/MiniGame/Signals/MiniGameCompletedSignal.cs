using PFound.Signaling;

namespace PFound.HubApp.MiniGame.Signals
{
	/// <summary>
	/// Queued by <see cref="MiniGameHost"/> right BEFORE <c>OnExit</c> + scene unload, so analytics can
	/// still query <c>HubApp</c> services for final state. Carries no payload — listeners look up the
	/// just-finished game id from the host.
	/// </summary>
	public class MiniGameCompletedSignal : SignalBase { }
}
