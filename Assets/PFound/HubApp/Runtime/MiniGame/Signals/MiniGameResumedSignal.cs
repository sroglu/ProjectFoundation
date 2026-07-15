using PFound.Signaling;

namespace PFound.HubApp.MiniGame.Signals
{
	/// <summary>Queued by <see cref="MiniGameHost.Resume"/> when the mini-game returns from pause.</summary>
	public class MiniGameResumedSignal : SignalBase { }
}
