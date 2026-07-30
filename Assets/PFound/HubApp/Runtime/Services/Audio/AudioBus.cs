namespace PFound.HubApp.Services.Audio
{
	/// <summary>
	/// Identifies an AudioMixer bus. Each value maps to an exposed parameter on the
	/// PlaynestAudioMixer: "<c>{Name}Volume</c>" (e.g. <c>MusicVolume</c>).
	/// </summary>
	public enum AudioBus
	{
		Master,
		Music,
		SFX,
		Voice,
		UI
	}
}
