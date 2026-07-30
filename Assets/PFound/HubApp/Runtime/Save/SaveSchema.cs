using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PFound.HubApp.Save
{
	// Root JSON shape for save.json. Mirrors HubApp_TDD §8 (Data Models).
	// Fail-fast convention: every collection initialized to empty in default ctors so callers
	// never have to null-check Schema.Profile.Profiles, Schema.Shared[id].Badges, etc. If any
	// of these IS null at runtime, deserialization broke an invariant — let the next access NRE.
	[JsonObject(MemberSerialization.OptOut)]
	public class SaveSchema
	{
		public const int CurrentSchemaVersion = 1;

		public int SchemaVersion;
		public ProfileSection Profile;
		public Dictionary<string, SharedProfileData> Shared;
		public Dictionary<string, GamesProfileData> Games;
		public SettingsData Settings;

		public SaveSchema()
		{
			SchemaVersion = CurrentSchemaVersion;
			Profile = new ProfileSection();
			Shared = new Dictionary<string, SharedProfileData>();
			Games = new Dictionary<string, GamesProfileData>();
			Settings = new SettingsData();
		}
	}

	[JsonObject(MemberSerialization.OptOut)]
	public class ProfileSection
	{
		public string ActiveProfileId;
		public List<ProfileEntry> Profiles;

		public ProfileSection()
		{
			ActiveProfileId = null;
			Profiles = new List<ProfileEntry>();
		}
	}

	[JsonObject(MemberSerialization.OptOut)]
	public class ProfileEntry
	{
		public string Id;
		public string DisplayName;
		public string AvatarId;
		public DateTime CreatedAt;
		public string AgeBand;
	}

	[JsonObject(MemberSerialization.OptOut)]
	public class SharedProfileData
	{
		public List<EarnedBadge> Badges;
		public List<EarnedSticker> Stickers;
		public List<PhotoEntry> Photos;
		public long TotalPlaytimeSeconds;

		public SharedProfileData()
		{
			Badges = new List<EarnedBadge>();
			Stickers = new List<EarnedSticker>();
			Photos = new List<PhotoEntry>();
			TotalPlaytimeSeconds = 0;
		}
	}

	[JsonObject(MemberSerialization.OptOut)]
	public class GamesProfileData
	{
		// Key: gameId. Value: arbitrary blob owned by that mini-game (set/read via ScopedSaveService).
		public Dictionary<string, JObject> PerGameState;

		public GamesProfileData()
		{
			PerGameState = new Dictionary<string, JObject>();
		}
	}

	[JsonObject(MemberSerialization.OptOut)]
	public class SettingsData
	{
		public string Language;
		public float MusicVolume;
		public float SfxVolume;
		public float VoiceVolume;
		public int ScreenTimeLimitMinutes;
		public bool AnalyticsOptIn;

		public SettingsData()
		{
			Language = "en";
			MusicVolume = 1.0f;
			SfxVolume = 1.0f;
			VoiceVolume = 1.0f;
			ScreenTimeLimitMinutes = 0;
			AnalyticsOptIn = false;
		}
	}

	[JsonObject(MemberSerialization.OptOut)]
	public class EarnedBadge
	{
		public string Id;
		public DateTime EarnedAt;
		public string SourceGame;
	}

	[JsonObject(MemberSerialization.OptOut)]
	public class EarnedSticker
	{
		public string Id;
		public DateTime EarnedAt;
		public string SourceGame;
	}

	[JsonObject(MemberSerialization.OptOut)]
	public class PhotoEntry
	{
		public string Id;
		public string Filename;
		public string SourceGame;
		public string SceneTag;
		public DateTime TakenAt;
		public int Width;
		public int Height;
	}
}
