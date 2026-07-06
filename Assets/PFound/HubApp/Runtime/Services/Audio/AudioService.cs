using System;
using System.Collections.Generic;
using PFound.HubApp.Save;
using UnityEngine;
using UnityEngine.Audio;

namespace PFound.HubApp.Services.Audio
{
	/// <summary>
	/// Concrete <see cref="IAudioService"/>. Wraps Unity's <see cref="AudioMixer"/> with
	/// per-bus volume persistence via <see cref="ISaveService"/> and snapshot-based voice
	/// ducking (HubApp_TDD §6.2).
	/// </summary>
	/// <remarks>
	/// The service is the source of truth for volume levels — it tracks linear volumes
	/// internally and pushes them to the mixer as a side effect. This avoids requiring
	/// exposed parameters for <c>GetFloat</c> read-back and keeps the service testable
	/// with bare mixer assets.
	/// Volume mapping: the mixer uses dB (−80..0), the public API uses linear (0..1).
	/// Conversion: <c>dB = 20 * log10(linear)</c>, clamped so 0 → −80 dB (silence).
	/// Exposed parameter naming convention: "<c>{BusName}Volume</c>" — e.g. "MusicVolume".
	/// </remarks>
	public class AudioService : IAudioService
	{
		private const float MinDb = -80f;
		private const float DuckTransitionSeconds = 0.3f;

		private readonly ISaveService _save;
		private readonly AudioMixer _mixer;
		private readonly AudioMixerSnapshot _defaultSnapshot;
		private readonly AudioMixerSnapshot _duckingSnapshot;

		private readonly Dictionary<AudioBus, float> _volumes = new Dictionary<AudioBus, float>();
		private bool _isDucking;
		private bool _isMuted;

		public AudioService(ISaveService save, AudioMixer mixer,
			AudioMixerSnapshot defaultSnapshot, AudioMixerSnapshot duckingSnapshot)
		{
			if (save == null) throw new ArgumentNullException(nameof(save));
			if (mixer == null) throw new ArgumentNullException(nameof(mixer));
			if (defaultSnapshot == null) throw new ArgumentNullException(nameof(defaultSnapshot));
			if (duckingSnapshot == null) throw new ArgumentNullException(nameof(duckingSnapshot));

			_save = save;
			_mixer = mixer;
			_defaultSnapshot = defaultSnapshot;
			_duckingSnapshot = duckingSnapshot;

			// Initialize all buses to full volume, then override from save.
			foreach (AudioBus bus in Enum.GetValues(typeof(AudioBus)))
				_volumes[bus] = 1f;

			ApplySavedVolumes();
		}

		public bool IsDucking => _isDucking;
		public bool IsMuted => _isMuted;

		public float GetVolume(AudioBus bus) => _volumes[bus];

		public void SetVolume(AudioBus bus, float linear)
		{
			linear = Mathf.Clamp01(linear);
			_volumes[bus] = linear;
			ApplyToMixer(bus, linear);
			PersistIfApplicable(bus, linear);
		}

		public void StartDucking()
		{
			if (_isDucking) return;
			_isDucking = true;
			_duckingSnapshot.TransitionTo(DuckTransitionSeconds);
		}

		public void StopDucking()
		{
			if (!_isDucking) return;
			_isDucking = false;
			_defaultSnapshot.TransitionTo(DuckTransitionSeconds);
		}

		public void SetMuted(bool muted)
		{
			_isMuted = muted;
			AudioListener.volume = muted ? 0f : 1f;
		}

		private void ApplySavedVolumes()
		{
			var s = _save.Schema.Settings;
			SetVolume(AudioBus.Music, s.MusicVolume);
			SetVolume(AudioBus.SFX, s.SfxVolume);
			SetVolume(AudioBus.Voice, s.VoiceVolume);
		}

		private void ApplyToMixer(AudioBus bus, float linear)
		{
			// SetFloat silently no-ops if the parameter isn't exposed — this is fine
			// because the parameter naming convention is documented and validated at
			// integration time (Faz 2 shell setup), not at service construction.
			_mixer.SetFloat(ParamName(bus), LinearToDb(linear));
		}

		private void PersistIfApplicable(AudioBus bus, float linear)
		{
			var s = _save.Schema.Settings;
			switch (bus)
			{
				case AudioBus.Music: s.MusicVolume = linear; break;
				case AudioBus.SFX:   s.SfxVolume = linear;   break;
				case AudioBus.Voice: s.VoiceVolume = linear;  break;
			}
		}

		private static string ParamName(AudioBus bus) => bus + "Volume";

		private static float LinearToDb(float linear)
		{
			if (linear <= 0f) return MinDb;
			return Mathf.Max(MinDb, 20f * Mathf.Log10(linear));
		}
	}
}
