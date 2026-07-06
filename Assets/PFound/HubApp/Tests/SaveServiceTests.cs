using System;
using System.IO;
using PFound.HubApp.Save;
using Newtonsoft.Json;
using NUnit.Framework;

namespace PFound.HubApp.Tests
{
	[TestFixture]
	public class SaveServiceTests
	{
		private string _saveRoot;

		[SetUp]
		public void Setup()
		{
			_saveRoot = Path.Combine(Path.GetTempPath(), "SaveServiceTests_" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_saveRoot);
		}

		[TearDown]
		public void Teardown()
		{
			if (Directory.Exists(_saveRoot))
				Directory.Delete(_saveRoot, recursive: true);
		}

		// ─── Ctor (boundary validation) ──────────────────

		[Test]
		public void Ctor_NullSaveRoot_Throws()
		{
			Assert.Throws<ArgumentException>(() => new SaveService(null));
		}

		[Test]
		public void Ctor_EmptySaveRoot_Throws()
		{
			Assert.Throws<ArgumentException>(() => new SaveService(""));
		}

		// ─── Load: first launch ──────────────────────────

		[Test]
		public void Load_FileMissing_PopulatesFreshDefaultSchema()
		{
			var svc = new SaveService(_saveRoot);
			svc.Load();

			Assert.NotNull(svc.Schema);
			Assert.AreEqual(SaveSchema.CurrentSchemaVersion, svc.Schema.SchemaVersion);
			Assert.IsEmpty(svc.Schema.Profile.Profiles);
		}

		[Test]
		public void SchemaIsNullBeforeLoad_FailFastOnAccess()
		{
			var svc = new SaveService(_saveRoot);
			Assert.IsNull(svc.Schema);
			// Per fail-fast philosophy, accessing svc.Schema.Profile here would NRE — that's
			// the intended signal that Load() was forgotten. We don't pre-guard with an
			// InvalidOperationException; the NRE at the access site is the diagnostic.
		}

		// ─── Save → Load roundtrip ───────────────────────

		[Test]
		public void Save_ThenLoad_RoundtripPreservesSchema()
		{
			var svc1 = new SaveService(_saveRoot);
			svc1.Load();
			svc1.Schema.Profile.ActiveProfileId = "p_001";
			svc1.Schema.Profile.Profiles.Add(new ProfileEntry
			{
				Id = "p_001",
				DisplayName = "Eli",
				AvatarId = "owl_03",
				CreatedAt = new DateTime(2026, 4, 16, 12, 0, 0, DateTimeKind.Utc),
				AgeBand = "4+"
			});
			svc1.Schema.Settings.Language = "tr";
			svc1.Schema.Settings.MusicVolume = 0.7f;
			svc1.Save();

			Assert.IsTrue(File.Exists(svc1.SavePath));

			var svc2 = new SaveService(_saveRoot);
			svc2.Load();
			Assert.AreEqual("p_001", svc2.Schema.Profile.ActiveProfileId);
			Assert.AreEqual(1, svc2.Schema.Profile.Profiles.Count);
			Assert.AreEqual("Eli", svc2.Schema.Profile.Profiles[0].DisplayName);
			Assert.AreEqual("tr", svc2.Schema.Settings.Language);
			Assert.AreEqual(0.7f, svc2.Schema.Settings.MusicVolume);
		}

		[Test]
		public void Flush_IsSynonymForSave_PersistsChanges()
		{
			var svc = new SaveService(_saveRoot);
			svc.Load();
			svc.Schema.Settings.SfxVolume = 0.42f;
			svc.Flush();

			var svc2 = new SaveService(_saveRoot);
			svc2.Load();
			Assert.AreEqual(0.42f, svc2.Schema.Settings.SfxVolume);
		}

		// ─── .bak fallback (end-to-end via FileTools) ───

		[Test]
		public void Load_MainFileDeleted_FallsBackToBak()
		{
			// Two saves: first creates main, second rotates first to .bak.
			var svc = new SaveService(_saveRoot);
			svc.Load();
			svc.Schema.Settings.Language = "tr";
			svc.Save();
			svc.Schema.Settings.Language = "en";
			svc.Save();

			// Simulate corruption / loss of the main file. .bak should still hold the v1 ("tr") state.
			File.Delete(svc.SavePath);
			Assert.IsTrue(File.Exists(svc.SavePath + ".bak"));

			var recovered = new SaveService(_saveRoot);
			recovered.Load();
			Assert.AreEqual("tr", recovered.Schema.Settings.Language);
		}

		// ─── Reset ───────────────────────────────────────

		[Test]
		public void Reset_RestoresInMemoryDefaults_DoesNotTouchDisk()
		{
			var svc = new SaveService(_saveRoot);
			svc.Load();
			svc.Schema.Settings.Language = "tr";
			svc.Save();

			svc.Reset();
			Assert.AreEqual("en", svc.Schema.Settings.Language);

			// Disk still has the pre-reset state.
			var svc2 = new SaveService(_saveRoot);
			svc2.Load();
			Assert.AreEqual("tr", svc2.Schema.Settings.Language);
		}

		// ─── Fail-fast: corruption surfaces, not silent ─

		[Test]
		public void Load_MalformedJson_ThrowsNotSilent()
		{
			File.WriteAllText(Path.Combine(_saveRoot, SaveService.SaveFileName), "{ this is not json");

			var svc = new SaveService(_saveRoot);
			Assert.Throws<JsonReaderException>(() => svc.Load());
		}

		[Test]
		public void Load_FutureSchemaVersion_ThrowsDowngradeError()
		{
			File.WriteAllText(
				Path.Combine(_saveRoot, SaveService.SaveFileName),
				@"{""SchemaVersion"":999,""Profile"":{},""Shared"":{},""Games"":{},""Settings"":{}}");

			var svc = new SaveService(_saveRoot);
			Assert.Throws<InvalidOperationException>(() => svc.Load());
		}

		[Test]
		public void Load_OlderSchemaVersion_NoMigrationsRegistered_Throws()
		{
			// SchemaVersion=0 (older than CurrentSchemaVersion=1) with empty migration pipeline
			// → MigrationPipeline must throw (gap from v0→v1). Verifies SaveService doesn't
			// silently accept an out-of-date save.
			File.WriteAllText(
				Path.Combine(_saveRoot, SaveService.SaveFileName),
				@"{""SchemaVersion"":0,""Profile"":{},""Shared"":{},""Games"":{},""Settings"":{}}");

			var svc = new SaveService(_saveRoot);
			Assert.Throws<InvalidOperationException>(() => svc.Load());
		}
	}
}
