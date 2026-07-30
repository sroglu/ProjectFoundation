using System;
using System.IO;
using PFound.HubApp.MiniGame.Signals;
using PFound.HubApp.Save;
using PFound.HubApp.Services.Badges;
using PFound.Signaling;
using NUnit.Framework;

namespace PFound.HubApp.Tests
{
	[TestFixture]
	public class BadgeServiceTests
	{
		private string _saveRoot;
		private SaveService _save;
		private SignalTracker _signals;
		private BadgeService _svc;
		private int _badgePersistedCount;

		[SetUp]
		public void Setup()
		{
			_saveRoot = Path.Combine(Path.GetTempPath(), "BadgeServiceTests_" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_saveRoot);

			_save = new SaveService(_saveRoot);
			_save.Load();

			_signals = new SignalTracker();
			_signals.AddListener<BadgePersistedSignal>(OnPersisted);

			_svc = new BadgeService(_save, _signals);
			_badgePersistedCount = 0;
		}

		[TearDown]
		public void Teardown()
		{
			_signals.RemoveListener<BadgePersistedSignal>(OnPersisted);
			if (Directory.Exists(_saveRoot))
				Directory.Delete(_saveRoot, recursive: true);
		}

		private void OnPersisted(SignalKey _) => _badgePersistedCount++;
		private void Pump() => _signals.EmitQueuedSignals();

		// ─── Ctor (boundary validation) ──────────────────

		[Test]
		public void Ctor_NullSave_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => new BadgeService(null, _signals));
		}

		[Test]
		public void Ctor_NullSignals_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => new BadgeService(_save, null));
		}

		// ─── Empty starting state ────────────────────────

		[Test]
		public void GetBadgesFor_UnknownProfile_ReturnsEmpty()
		{
			Assert.IsEmpty(_svc.GetBadgesFor("p_nope"));
		}

		[Test]
		public void HasBadge_UnknownProfile_ReturnsFalse()
		{
			Assert.IsFalse(_svc.HasBadge("p_nope", "first_camp"));
		}

		// ─── Award: first earn ───────────────────────────

		[Test]
		public void Award_NewBadge_ReturnsTrueAndPersists()
		{
			var earned = _svc.Award("p_001", "first_camp", "camping");

			Assert.IsTrue(earned);
			Assert.IsTrue(_svc.HasBadge("p_001", "first_camp"));
			Assert.AreEqual(1, _svc.GetBadgesFor("p_001").Count);
			Assert.AreEqual("first_camp", _svc.GetBadgesFor("p_001")[0].Id);
			Assert.AreEqual("camping", _svc.GetBadgesFor("p_001")[0].SourceGame);
		}

		[Test]
		public void Award_NewBadge_QueuesSignal()
		{
			_svc.Award("p_001", "first_camp", "camping");
			Pump();
			Assert.AreEqual(1, _badgePersistedCount);
		}

		[Test]
		public void Award_PersistsToSharedSubtree_AutoCreated()
		{
			Assert.IsFalse(_save.Schema.Shared.ContainsKey("p_001"));

			_svc.Award("p_001", "first_camp", "camping");

			Assert.IsTrue(_save.Schema.Shared.ContainsKey("p_001"));
			Assert.AreEqual(1, _save.Schema.Shared["p_001"].Badges.Count);
		}

		// ─── Award: idempotency ──────────────────────────

		[Test]
		public void Award_DuplicateBadge_ReturnsFalseAndNoSignal()
		{
			Assert.IsTrue(_svc.Award("p_001", "first_camp", "camping"));
			Pump();
			Assert.AreEqual(1, _badgePersistedCount);

			Assert.IsFalse(_svc.Award("p_001", "first_camp", "camping"));
			Pump();
			Assert.AreEqual(1, _badgePersistedCount);
			Assert.AreEqual(1, _svc.GetBadgesFor("p_001").Count);
		}

		// ─── Profile isolation ──────────────────────────

		[Test]
		public void Award_TwoProfiles_DoNotShareLedger()
		{
			_svc.Award("p_001", "first_camp", "camping");
			_svc.Award("p_002", "first_toss", "tossytoss");

			Assert.AreEqual(1, _svc.GetBadgesFor("p_001").Count);
			Assert.AreEqual(1, _svc.GetBadgesFor("p_002").Count);
			Assert.IsFalse(_svc.HasBadge("p_001", "first_toss"));
			Assert.IsFalse(_svc.HasBadge("p_002", "first_camp"));
		}

		// ─── Argument validation ────────────────────────

		[Test]
		public void Award_EmptyProfileId_Throws()
		{
			Assert.Throws<ArgumentException>(() => _svc.Award("", "first_camp", "camping"));
		}

		[Test]
		public void Award_EmptyBadgeId_Throws()
		{
			Assert.Throws<ArgumentException>(() => _svc.Award("p_001", "", "camping"));
		}

		[Test]
		public void Award_NullSourceGame_Allowed()
		{
			// Admin/debug tools may award without a game source.
			Assert.IsTrue(_svc.Award("p_001", "admin_grant", null));
			Assert.IsNull(_svc.GetBadgesFor("p_001")[0].SourceGame);
		}
	}
}
