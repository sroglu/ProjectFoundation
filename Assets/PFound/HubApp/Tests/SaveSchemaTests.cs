using System;
using System.Collections.Generic;
using PFound.HubApp.Save;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace PFound.HubApp.Tests
{
	[TestFixture]
	public class SaveSchemaTests
	{
		// ─── Default ctor ─────────────────────────────────

		[Test]
		public void Ctor_InitializesAllCollectionsToEmpty_NoNulls()
		{
			var schema = new SaveSchema();

			Assert.AreEqual(SaveSchema.CurrentSchemaVersion, schema.SchemaVersion);
			Assert.NotNull(schema.Profile);
			Assert.NotNull(schema.Profile.Profiles);
			Assert.IsEmpty(schema.Profile.Profiles);
			Assert.NotNull(schema.Shared);
			Assert.IsEmpty(schema.Shared);
			Assert.NotNull(schema.Games);
			Assert.IsEmpty(schema.Games);
			Assert.NotNull(schema.Settings);
		}

		[Test]
		public void SettingsData_DefaultCtor_HasUsableDefaults()
		{
			var s = new SettingsData();
			Assert.AreEqual("en", s.Language);
			Assert.AreEqual(1.0f, s.MusicVolume);
			Assert.AreEqual(1.0f, s.SfxVolume);
			Assert.AreEqual(1.0f, s.VoiceVolume);
			Assert.AreEqual(0, s.ScreenTimeLimitMinutes);
			Assert.IsFalse(s.AnalyticsOptIn);
		}

		// ─── JSON roundtrip ───────────────────────────────

		[Test]
		public void Roundtrip_Empty_DefaultSchema_PreservesShape()
		{
			var schema = new SaveSchema();
			var json = JsonConvert.SerializeObject(schema, Formatting.Indented);
			var back = JsonConvert.DeserializeObject<SaveSchema>(json);

			Assert.AreEqual(schema.SchemaVersion, back.SchemaVersion);
			Assert.NotNull(back.Profile);
			Assert.NotNull(back.Profile.Profiles);
			Assert.NotNull(back.Shared);
			Assert.NotNull(back.Games);
			Assert.NotNull(back.Settings);
		}

		[Test]
		public void Roundtrip_PopulatedProfile_SurvivesSerialization()
		{
			var schema = new SaveSchema();
			schema.Profile.ActiveProfileId = "p_001";
			schema.Profile.Profiles.Add(new ProfileEntry
			{
				Id = "p_001",
				DisplayName = "Ada",
				AvatarId = "fox_01",
				CreatedAt = new DateTime(2026, 4, 16, 10, 0, 0, DateTimeKind.Utc),
				AgeBand = "3-4"
			});

			var json = JsonConvert.SerializeObject(schema, Formatting.Indented);
			var back = JsonConvert.DeserializeObject<SaveSchema>(json);

			Assert.AreEqual("p_001", back.Profile.ActiveProfileId);
			Assert.AreEqual(1, back.Profile.Profiles.Count);
			var entry = back.Profile.Profiles[0];
			Assert.AreEqual("p_001", entry.Id);
			Assert.AreEqual("Ada", entry.DisplayName);
			Assert.AreEqual("fox_01", entry.AvatarId);
			Assert.AreEqual("3-4", entry.AgeBand);
			Assert.AreEqual(new DateTime(2026, 4, 16, 10, 0, 0, DateTimeKind.Utc), entry.CreatedAt.ToUniversalTime());
		}

		[Test]
		public void Roundtrip_SharedProfileData_PreservesAllCollections()
		{
			var schema = new SaveSchema();
			var shared = new SharedProfileData
			{
				TotalPlaytimeSeconds = 14320,
			};
			shared.Badges.Add(new EarnedBadge { Id = "first_camp", EarnedAt = DateTime.UtcNow, SourceGame = "camping" });
			shared.Stickers.Add(new EarnedSticker { Id = "sticker_fox", EarnedAt = DateTime.UtcNow, SourceGame = "tinytales" });
			shared.Photos.Add(new PhotoEntry { Id = "ph_abc", Filename = "ph_abc.jpg", SourceGame = "camping", SceneTag = "forest", TakenAt = DateTime.UtcNow, Width = 1920, Height = 1080 });
			schema.Shared["p_001"] = shared;

			var json = JsonConvert.SerializeObject(schema, Formatting.Indented);
			var back = JsonConvert.DeserializeObject<SaveSchema>(json);

			Assert.IsTrue(back.Shared.ContainsKey("p_001"));
			var bs = back.Shared["p_001"];
			Assert.AreEqual(14320, bs.TotalPlaytimeSeconds);
			Assert.AreEqual(1, bs.Badges.Count);
			Assert.AreEqual("first_camp", bs.Badges[0].Id);
			Assert.AreEqual(1, bs.Stickers.Count);
			Assert.AreEqual("sticker_fox", bs.Stickers[0].Id);
			Assert.AreEqual(1, bs.Photos.Count);
			Assert.AreEqual(1920, bs.Photos[0].Width);
		}

		[Test]
		public void Roundtrip_GamesProfileData_FreeFormJObjectBlob_Preserved()
		{
			var schema = new SaveSchema();
			var games = new GamesProfileData();
			games.PerGameState["tossytoss"] = JObject.Parse(@"{""level"":3,""highScore"":1240,""nested"":{""arr"":[1,2,3]}}");
			schema.Games["p_001"] = games;

			var json = JsonConvert.SerializeObject(schema, Formatting.Indented);
			var back = JsonConvert.DeserializeObject<SaveSchema>(json);

			Assert.IsTrue(back.Games.ContainsKey("p_001"));
			var bag = back.Games["p_001"].PerGameState["tossytoss"];
			Assert.AreEqual(3, (int)bag["level"]);
			Assert.AreEqual(1240, (int)bag["highScore"]);
			Assert.AreEqual(2, (int)bag["nested"]["arr"][1]);
		}
	}
}
