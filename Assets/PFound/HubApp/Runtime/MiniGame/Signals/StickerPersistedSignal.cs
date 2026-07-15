using PFound.Signaling;

namespace PFound.HubApp.MiniGame.Signals
{
	/// <summary>
	/// Mirror of <see cref="BadgePersistedSignal"/> for stickers. Fired only on the first earn for
	/// the active profile; duplicate awards are no-ops and emit nothing.
	/// </summary>
	public class StickerPersistedSignal : SignalBase { }
}
