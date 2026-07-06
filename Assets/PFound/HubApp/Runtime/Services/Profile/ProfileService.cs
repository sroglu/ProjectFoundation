using System;
using System.Collections.Generic;
using PFound.HubApp.MiniGame.Signals;
using PFound.HubApp.Save;
using PFound.Signaling;

namespace PFound.HubApp.Services.Profile
{
	/// <summary>
	/// Concrete <see cref="IProfileService"/>. Operates directly on the live
	/// <see cref="ISaveService.Schema"/> instance — there is no in-memory shadow copy. Callers
	/// that want changes persisted must invoke <c>SaveService.Flush()</c>; this service never
	/// auto-flushes because the Shell controls write timing (app pause / settings close / etc.).
	/// </summary>
	public class ProfileService : IProfileService
	{
		// Profile id format: "p_" + 8 lowercase hex chars (e.g. "p_a1b2c3d4"). Short enough to
		// log without leaking entropy; long enough that collisions across reasonable profile
		// counts (sibling use case = 2-4 profiles per device) are vanishingly unlikely.
		private const string ProfileIdPrefix = "p_";

		private readonly ISaveService _save;
		private readonly SignalTracker _signals;

		public ProfileService(ISaveService save, SignalTracker signals)
		{
			if (save == null) throw new ArgumentNullException(nameof(save));
			if (signals == null) throw new ArgumentNullException(nameof(signals));

			_save = save;
			_signals = signals;
		}

		public string ActiveProfileId => _save.Schema.Profile.ActiveProfileId;

		public IReadOnlyList<ProfileEntry> Profiles => _save.Schema.Profile.Profiles;

		public bool HasProfile(string profileId)
		{
			if (string.IsNullOrEmpty(profileId))
				throw new ArgumentException("profileId must not be empty.", nameof(profileId));

			return FindIndex(profileId) >= 0;
		}

		public ProfileEntry GetProfile(string profileId)
		{
			if (string.IsNullOrEmpty(profileId))
				throw new ArgumentException("profileId must not be empty.", nameof(profileId));

			var index = FindIndex(profileId);
			if (index < 0)
				throw new KeyNotFoundException($"Profile '{profileId}' does not exist. Use HasProfile() to gate.");

			return _save.Schema.Profile.Profiles[index];
		}

		public ProfileEntry Create(string displayName, string ageBand, string avatarId = null)
		{
			if (string.IsNullOrWhiteSpace(displayName))
				throw new ArgumentException("displayName must not be empty.", nameof(displayName));
			if (string.IsNullOrWhiteSpace(ageBand))
				throw new ArgumentException("ageBand must not be empty.", nameof(ageBand));

			var entry = new ProfileEntry
			{
				Id = GenerateProfileId(),
				DisplayName = displayName,
				AvatarId = avatarId,
				AgeBand = ageBand,
				CreatedAt = DateTime.UtcNow
			};

			_save.Schema.Profile.Profiles.Add(entry);
			return entry;
		}

		public void SetActive(string profileId)
		{
			if (string.IsNullOrEmpty(profileId))
				throw new ArgumentException("profileId must not be empty.", nameof(profileId));

			if (FindIndex(profileId) < 0)
				throw new KeyNotFoundException($"Cannot activate '{profileId}' — profile does not exist.");

			// No-op + no signal if already active. Signals are notifications of *change*; firing
			// on identity-set would burn UI cycles refreshing for nothing.
			if (string.Equals(_save.Schema.Profile.ActiveProfileId, profileId, StringComparison.Ordinal))
				return;

			_save.Schema.Profile.ActiveProfileId = profileId;
			_signals.Queue<ProfileSelectedSignal>(causer: null);
		}

		public void ClearActive()
		{
			if (_save.Schema.Profile.ActiveProfileId == null)
				return;

			_save.Schema.Profile.ActiveProfileId = null;
			_signals.Queue<ProfileSelectedSignal>(causer: null);
		}

		public void Delete(string profileId)
		{
			if (string.IsNullOrEmpty(profileId))
				throw new ArgumentException("profileId must not be empty.", nameof(profileId));

			var index = FindIndex(profileId);
			if (index < 0)
				throw new KeyNotFoundException($"Cannot delete '{profileId}' — profile does not exist.");

			_save.Schema.Profile.Profiles.RemoveAt(index);
			_save.Schema.Shared.Remove(profileId);
			_save.Schema.Games.Remove(profileId);

			// Signal once at the end if we lost the active selection — UI must redirect to
			// ProfileSelect. Setting to null directly (not via ClearActive) so we emit the
			// signal exactly once for the whole delete operation.
			if (string.Equals(_save.Schema.Profile.ActiveProfileId, profileId, StringComparison.Ordinal))
			{
				_save.Schema.Profile.ActiveProfileId = null;
				_signals.Queue<ProfileSelectedSignal>(causer: null);
			}
		}

		private int FindIndex(string profileId)
		{
			var profiles = _save.Schema.Profile.Profiles;
			for (var i = 0; i < profiles.Count; i++)
			{
				if (string.Equals(profiles[i].Id, profileId, StringComparison.Ordinal))
					return i;
			}
			return -1;
		}

		private static string GenerateProfileId()
		{
			// Guid.N → 32 hex chars; first 8 give us 4 bytes of entropy ≈ 4.3B keyspace.
			// More than sufficient for per-device profile sets (2-4 typical).
			return ProfileIdPrefix + Guid.NewGuid().ToString("N").Substring(0, 8);
		}
	}
}
