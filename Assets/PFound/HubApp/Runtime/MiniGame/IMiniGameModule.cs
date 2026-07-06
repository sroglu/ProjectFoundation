namespace PFound.HubApp.MiniGame
{
	/// <summary>
	/// Contract every Playnest mini-game implements (HubApp_TDD §7.1). The shell guarantees the
	/// services in <see cref="MiniGameContext"/>; mini-games may not assume any other shell state.
	/// </summary>
	/// <remarks>
	/// Lifecycle: <see cref="Initialize"/> fires once after the additive scene load. <see cref="Pause"/>
	/// / <see cref="Resume"/> bracket app-background or parent-gate periods. <see cref="OnExit"/> is the
	/// last call before the scene is unloaded — release pooled handles, persist final state via
	/// <c>MiniGameContext.Save</c>, etc.
	/// Implementations are typically <c>MonoBehaviour</c>s sitting on a root GameObject of the
	/// mini-game scene, but this contract has no MonoBehaviour requirement so unit tests can stub.
	/// </remarks>
	public interface IMiniGameModule
	{
		/// <summary>Stable lower_snake_case identifier (e.g. "tossytoss"). Used for save scoping and analytics tags.</summary>
		string GameId { get; }

		void Initialize(MiniGameContext context);

		void Pause();

		void Resume();

		/// <summary>
		/// Last-chance cleanup. Must NOT throw — the host's GC sweep is unconditional and cannot
		/// recover from a partial unload. Catch and log instead.
		/// </summary>
		void OnExit();
	}
}
