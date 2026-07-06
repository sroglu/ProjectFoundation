namespace PFound.HubApp.Services.Audio
{
	/// <summary>
	/// Manages the global AudioMixer: per-bus volume (persisted to save), voice ducking
	/// snapshot transitions, and background mute. Mini-games access this through
	/// <see cref="MiniGame.MiniGameContext"/> to respect the player's volume preferences.
	/// </summary>
	/// <remarks>
	/// This service controls the mixer, not individual clips. Clip playback is the
	/// responsibility of each mini-game or the Shell — the service just ensures the
	/// mixer buses are routed at the right levels.
	/// </remarks>
	public interface IAudioService
	{
		/// <summary>Current linear volume [0..1] for the given bus.</summary>
		float GetVolume(AudioBus bus);

		/// <summary>
		/// Sets the linear volume [0..1] for <paramref name="bus"/>. Clamped to [0..1].
		/// Persisted volumes (Music, SFX, Voice) are written to <c>SaveSchema.Settings</c>
		/// immediately — caller is responsible for flushing the save file when appropriate.
		/// </summary>
		void SetVolume(AudioBus bus, float linear);

		/// <summary>
		/// Transitions to the VoiceDucking snapshot (Music −6 dB). Call when narration
		/// starts. Idempotent — extra calls while already ducking are no-ops.
		/// </summary>
		void StartDucking();

		/// <summary>
		/// Transitions back to the default snapshot. Call when narration ends.
		/// Idempotent — extra calls while not ducking are no-ops.
		/// </summary>
		void StopDucking();

		/// <summary>Whether ducking is currently active.</summary>
		bool IsDucking { get; }

		/// <summary>
		/// Mutes/unmutes the <c>AudioListener</c>. Called by the Shell on
		/// <c>Application.focusChanged</c> — when the app loses focus, audio stops;
		/// when it regains focus, audio resumes at the last volume levels.
		/// </summary>
		void SetMuted(bool muted);

		/// <summary>Whether the listener is currently muted (background).</summary>
		bool IsMuted { get; }
	}
}
