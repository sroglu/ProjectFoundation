using System;
using System.IO;
using PFound.HubApp.MiniGame.Signals;
using PFound.HubApp.Save;
using PFound.HubApp.Services.Stickers;
using PFound.Signaling;
using NUnit.Framework;

namespace PFound.HubApp.Tests
{
	[TestFixture]
	public class StickerServiceTests
	{
		private string _saveRoot;
		private SaveService _save;
		private SignalTracker _signals;
		private StickerService _svc;
		private int _stickerPersistedCount;

		[SetUp]
		public void Setup()
		{
			_saveRoot = Path.Combine(Path.GetTempPath(), "StickerServiceTests_" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_saveRoot);

			_save = new SaveService(_saveRoot);
			_save.Load();

			_signals = new SignalTracker();
			_signals.AddListener<StickerPersistedSignal>(OnPersisted);

			_svc = new StickerService(_save, _signals);
			_stickerPersistedCount = 0;
		}

		[TearDown]
		public void Teardown()
		{
			_signals.RemoveListener<StickerPersistedSignal>(OnPersisted);
			if (Directory.Exists(_saveRoot))
				Directory.Delete(_saveRoot, recursive: true);
		}

		private void OnPersisted(SignalKey _) => _stickerPersistedCount++;
		private void Pump() => _signals.EmitQueuedSignals();

		// ─── Ctor (boundary validation) ──────────────────

		[Test]
		public void Ctor_NullSave_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => new StickerService(null, _signals));
		}

		[Test]
		public void Ctor_NullSignals_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => new StickerService(_save, null));
		}

		// ─── Empty starting state ────────────────────────

		[Test]
		public void GetStickersFor_UnknownProfile_ReturnsEmpty()
		{
			Assert.IsEmpty(_svc.GetStickersFor("p_nope"));
		}

		// ─── Award: first earn ───────────────────────────

		[Test]
		public void Award_NewSticker_ReturnsTrueAndPersists()
		{
			Assert.IsTrue(_svc.Award("p_001", "sticker_fox", "tinytales"));
			Assert.IsTrue(_svc.HasSticker("p_001", "sticker_fox"));
			Assert.AreEqual(1, _svc.GetStickersFor("p_001").Count);
			Assert.AreEqual("tinytales", _svc.GetStickersFor("p_001")[0].SourceGame);
		}

		[Test]
		public void Award_NewSticker_QueuesSignal()
		{
			_svc.Award("p_001", "sticker_fox", "tinytales");
			Pump();
			Assert.AreEqual(1, _stickerPersistedCount);
		}

		// ─── Award: idempotency ──────────────────────────

		[Test]
		public void Award_Duplicate_ReturnsFalseAndNoSignal()
		{
			Assert.IsTrue(_svc.Award("p_001", "sticker_fox", "tinytales"));
			Pump();
			Assert.AreEqual(1, _stickerPersistedCount);

			Assert.IsFalse(_svc.Award("p_001", "sticker_fox", "tinytales"));
			Pump();
			Assert.AreEqual(1, _stickerPersistedCount);
			Assert.AreEqual(1, _svc.GetStickersFor("p_001").Count);
		}

		// ─── Profile isolation ──────────────────────────

		[Test]
		public void Award_TwoProfiles_DoNotShareLedger()
		{
			_svc.Award("p_001", "sticker_fox", "tinytales");
			_svc.Award("p_002", "sticker_owl", "camping");

			Assert.AreEqual(1, _svc.GetStickersFor("p_001").Count);
			Assert.AreEqual(1, _svc.GetStickersFor("p_002").Count);
			Assert.IsFalse(_svc.HasSticker("p_001", "sticker_owl"));
		}

		// ─── Argument validation ────────────────────────

		[Test]
		public void Award_EmptyProfileId_Throws()
		{
			Assert.Throws<ArgumentException>(() => _svc.Award("", "sticker_fox", "tinytales"));
		}

		[Test]
		public void Award_EmptyStickerId_Throws()
		{
			Assert.Throws<ArgumentException>(() => _svc.Award("p_001", "", "tinytales"));
		}
	}
}
