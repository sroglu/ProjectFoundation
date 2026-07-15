using System;
using System.Collections.Generic;
using System.IO;
using PFound.HubApp.MiniGame.Signals;
using PFound.HubApp.Save;
using PFound.HubApp.Services.Profile;
using PFound.Signaling;
using NUnit.Framework;

namespace PFound.HubApp.Tests
{
	[TestFixture]
	public class ProfileServiceTests
	{
		private string _saveRoot;
		private SaveService _save;
		private SignalTracker _signals;
		private ProfileService _svc;
		private int _profileSelectedSignalCount;

		[SetUp]
		public void Setup()
		{
			_saveRoot = Path.Combine(Path.GetTempPath(), "ProfileServiceTests_" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_saveRoot);

			_save = new SaveService(_saveRoot);
			_save.Load();

			_signals = new SignalTracker();
			_signals.AddListener<ProfileSelectedSignal>(OnProfileSelected);

			_svc = new ProfileService(_save, _signals);
			_profileSelectedSignalCount = 0;
		}

		[TearDown]
		public void Teardown()
		{
			_signals.RemoveListener<ProfileSelectedSignal>(OnProfileSelected);
			if (Directory.Exists(_saveRoot))
				Directory.Delete(_saveRoot, recursive: true);
		}

		private void OnProfileSelected(SignalKey _) => _profileSelectedSignalCount++;

		// Pump must be called between an action that Queue<T>s a signal and any assert that
		// counts emissions — the SignalTracker is queue+flush, not immediate.
		private void Pump() => _signals.EmitQueuedSignals();

		// ─── Ctor (boundary validation) ──────────────────

		[Test]
		public void Ctor_NullSave_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => new ProfileService(null, _signals));
		}

		[Test]
		public void Ctor_NullSignals_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => new ProfileService(_save, null));
		}

		// ─── Empty starting state ────────────────────────

		[Test]
		public void Initial_ActiveProfileIsNull_AndProfilesEmpty()
		{
			Assert.IsNull(_svc.ActiveProfileId);
			Assert.IsEmpty(_svc.Profiles);
		}

		// ─── Create ──────────────────────────────────────

		[Test]
		public void Create_AssignsIdAndAppendsToProfilesList()
		{
			var entry = _svc.Create("Eli", "4+", "owl_03");

			Assert.IsTrue(entry.Id.StartsWith("p_"));
			Assert.AreEqual("Eli", entry.DisplayName);
			Assert.AreEqual("4+", entry.AgeBand);
			Assert.AreEqual("owl_03", entry.AvatarId);
			Assert.AreEqual(1, _svc.Profiles.Count);
			Assert.AreSame(entry, _svc.Profiles[0]);
		}

		[Test]
		public void Create_TwoProfiles_GeneratesDistinctIds()
		{
			var a = _svc.Create("Eli", "4+");
			var b = _svc.Create("Bo", "3-4");

			Assert.AreNotEqual(a.Id, b.Id);
			Assert.AreEqual(2, _svc.Profiles.Count);
		}

		[Test]
		public void Create_EmptyDisplayName_Throws()
		{
			Assert.Throws<ArgumentException>(() => _svc.Create("", "4+"));
			Assert.Throws<ArgumentException>(() => _svc.Create("   ", "4+"));
			Assert.Throws<ArgumentException>(() => _svc.Create(null, "4+"));
		}

		[Test]
		public void Create_EmptyAgeBand_Throws()
		{
			Assert.Throws<ArgumentException>(() => _svc.Create("Eli", ""));
			Assert.Throws<ArgumentException>(() => _svc.Create("Eli", null));
		}

		[Test]
		public void Create_DoesNotEmitSignal()
		{
			_svc.Create("Eli", "4+");
			Pump();
			Assert.AreEqual(0, _profileSelectedSignalCount);
		}

		// ─── HasProfile / GetProfile (fail-fast) ────────

		[Test]
		public void HasProfile_UnknownId_ReturnsFalse()
		{
			Assert.IsFalse(_svc.HasProfile("p_nope"));
		}

		[Test]
		public void GetProfile_UnknownId_Throws()
		{
			Assert.Throws<KeyNotFoundException>(() => _svc.GetProfile("p_nope"));
		}

		[Test]
		public void GetProfile_KnownId_ReturnsEntry()
		{
			var created = _svc.Create("Eli", "4+");
			var fetched = _svc.GetProfile(created.Id);
			Assert.AreSame(created, fetched);
		}

		// ─── SetActive ───────────────────────────────────

		[Test]
		public void SetActive_KnownId_UpdatesActiveAndEmitsSignal()
		{
			var entry = _svc.Create("Eli", "4+");

			_svc.SetActive(entry.Id);
			Pump();

			Assert.AreEqual(entry.Id, _svc.ActiveProfileId);
			Assert.AreEqual(1, _profileSelectedSignalCount);
		}

		[Test]
		public void SetActive_UnknownId_Throws_NoSignal()
		{
			Assert.Throws<KeyNotFoundException>(() => _svc.SetActive("p_nope"));
			Pump();
			Assert.AreEqual(0, _profileSelectedSignalCount);
		}

		[Test]
		public void SetActive_Same_NoOpAndNoSignal()
		{
			var entry = _svc.Create("Eli", "4+");
			_svc.SetActive(entry.Id);
			Pump();
			Assert.AreEqual(1, _profileSelectedSignalCount);

			_svc.SetActive(entry.Id);
			Pump();
			Assert.AreEqual(1, _profileSelectedSignalCount, "Second SetActive to the same id must not re-emit.");
		}

		// ─── ClearActive ─────────────────────────────────

		[Test]
		public void ClearActive_WhenActive_ClearsAndEmitsSignal()
		{
			var entry = _svc.Create("Eli", "4+");
			_svc.SetActive(entry.Id);
			Pump();

			_svc.ClearActive();
			Pump();

			Assert.IsNull(_svc.ActiveProfileId);
			Assert.AreEqual(2, _profileSelectedSignalCount);
		}

		[Test]
		public void ClearActive_WhenAlreadyNull_NoSignal()
		{
			_svc.ClearActive();
			Pump();
			Assert.AreEqual(0, _profileSelectedSignalCount);
		}

		// ─── Delete ──────────────────────────────────────

		[Test]
		public void Delete_RemovesProfileAndDownstreamSections()
		{
			var entry = _svc.Create("Eli", "4+");
			_save.Schema.Shared[entry.Id] = new SharedProfileData();
			_save.Schema.Games[entry.Id] = new GamesProfileData();

			_svc.Delete(entry.Id);

			Assert.IsEmpty(_svc.Profiles);
			Assert.IsFalse(_save.Schema.Shared.ContainsKey(entry.Id));
			Assert.IsFalse(_save.Schema.Games.ContainsKey(entry.Id));
		}

		[Test]
		public void Delete_ActiveProfile_ClearsActiveAndEmitsSignal()
		{
			var entry = _svc.Create("Eli", "4+");
			_svc.SetActive(entry.Id);
			Pump();
			Assert.AreEqual(1, _profileSelectedSignalCount);

			_svc.Delete(entry.Id);
			Pump();

			Assert.IsNull(_svc.ActiveProfileId);
			Assert.AreEqual(2, _profileSelectedSignalCount);
		}

		[Test]
		public void Delete_NonActiveProfile_DoesNotEmitSignal()
		{
			var keep = _svc.Create("Eli", "4+");
			var drop = _svc.Create("Bo", "3-4");
			_svc.SetActive(keep.Id);
			Pump();
			Assert.AreEqual(1, _profileSelectedSignalCount);

			_svc.Delete(drop.Id);
			Pump();

			Assert.AreEqual(keep.Id, _svc.ActiveProfileId);
			Assert.AreEqual(1, _profileSelectedSignalCount);
		}

		[Test]
		public void Delete_UnknownId_Throws()
		{
			Assert.Throws<KeyNotFoundException>(() => _svc.Delete("p_nope"));
		}
	}
}
