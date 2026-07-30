using System;
using System.IO;
using System.Linq;
using PFound.HubApp.MiniGame.Signals;
using PFound.HubApp.Save;
using PFound.HubApp.Services.PhotoAlbum;
using PFound.Signaling;
using NUnit.Framework;
using UnityEngine;

namespace PFound.HubApp.Tests
{
	[TestFixture]
	public class PhotoAlbumServiceTests
	{
		private string _root;
		private string _saveRoot;
		private string _photosRoot;
		private SaveService _save;
		private SignalTracker _signals;
		private PhotoAlbumService _svc;
		private int _photoCapturedCount;

		[SetUp]
		public void Setup()
		{
			_root = Path.Combine(Path.GetTempPath(), "PhotoAlbumTests_" + Guid.NewGuid().ToString("N"));
			_saveRoot = Path.Combine(_root, "save");
			_photosRoot = Path.Combine(_root, "photos");
			Directory.CreateDirectory(_saveRoot);
			Directory.CreateDirectory(_photosRoot);

			_save = new SaveService(_saveRoot);
			_save.Load();

			_signals = new SignalTracker();
			_signals.AddListener<PhotoCapturedSignal>(OnCaptured);

			_svc = new PhotoAlbumService(_save, _signals, _photosRoot);
			_photoCapturedCount = 0;
		}

		[TearDown]
		public void Teardown()
		{
			_signals.RemoveListener<PhotoCapturedSignal>(OnCaptured);
			if (Directory.Exists(_root))
				Directory.Delete(_root, recursive: true);
		}

		private void OnCaptured(SignalKey _) => _photoCapturedCount++;
		private void Pump() => _signals.EmitQueuedSignals();

		// EditMode tests can construct Texture2D directly; no GPU upload is required for
		// EncodeToJPG which reads from CPU pixel data.
		private static Texture2D MakeTinyTexture(int w = 16, int h = 16)
		{
			var t = new Texture2D(w, h, TextureFormat.RGB24, mipChain: false);
			var pixels = new Color32[w * h];
			for (var i = 0; i < pixels.Length; i++)
				pixels[i] = new Color32((byte)(i % 256), 128, 64, 255);
			t.SetPixels32(pixels);
			t.Apply(updateMipmaps: false);
			return t;
		}

		// ─── Ctor (boundary validation) ──────────────────

		[Test]
		public void Ctor_NullSave_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => new PhotoAlbumService(null, _signals, _photosRoot));
		}

		[Test]
		public void Ctor_NullSignals_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => new PhotoAlbumService(_save, null, _photosRoot));
		}

		[Test]
		public void Ctor_EmptyPhotosRoot_Throws()
		{
			Assert.Throws<ArgumentException>(() => new PhotoAlbumService(_save, _signals, ""));
		}

		[Test]
		public void Ctor_ZeroMaxPhotos_Throws()
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => new PhotoAlbumService(_save, _signals, _photosRoot, maxPhotos: 0));
		}

		// ─── Capture: happy path ─────────────────────────

		[Test]
		public void Capture_WritesJpegFile()
		{
			var tex = MakeTinyTexture();
			var entry = _svc.Capture("p_001", tex, "camping", "forest");

			var path = _svc.GetPhotoPath("p_001", entry);
			Assert.IsTrue(File.Exists(path));

			var bytes = File.ReadAllBytes(path);
			Assert.Greater(bytes.Length, 0);
			// JPEG SOI marker = 0xFF 0xD8.
			Assert.AreEqual(0xFF, bytes[0]);
			Assert.AreEqual(0xD8, bytes[1]);

			UnityEngine.Object.DestroyImmediate(tex);
		}

		[Test]
		public void Capture_AppendsEntryToSave()
		{
			var tex = MakeTinyTexture(32, 24);
			var entry = _svc.Capture("p_001", tex, "camping", "forest");

			Assert.AreEqual(1, _svc.GetPhotosFor("p_001").Count);
			var stored = _svc.GetPhotosFor("p_001")[0];
			Assert.AreSame(entry, stored);
			Assert.AreEqual("camping", stored.SourceGame);
			Assert.AreEqual("forest", stored.SceneTag);
			Assert.AreEqual(32, stored.Width);
			Assert.AreEqual(24, stored.Height);
			Assert.IsTrue(stored.Id.StartsWith("ph_"));
			Assert.AreEqual(stored.Id + ".jpg", stored.Filename);

			UnityEngine.Object.DestroyImmediate(tex);
		}

		[Test]
		public void Capture_QueuesPhotoCapturedSignal()
		{
			var tex = MakeTinyTexture();
			_svc.Capture("p_001", tex, "camping", "forest");
			Pump();
			Assert.AreEqual(1, _photoCapturedCount);

			UnityEngine.Object.DestroyImmediate(tex);
		}

		[Test]
		public void Capture_FirstPhoto_AutoCreatesSharedSubtreeAndDir()
		{
			Assert.IsFalse(_save.Schema.Shared.ContainsKey("p_001"));
			Assert.IsFalse(Directory.Exists(Path.Combine(_photosRoot, "p_001")));

			var tex = MakeTinyTexture();
			_svc.Capture("p_001", tex, "camping", "forest");

			Assert.IsTrue(_save.Schema.Shared.ContainsKey("p_001"));
			Assert.IsTrue(Directory.Exists(Path.Combine(_photosRoot, "p_001")));

			UnityEngine.Object.DestroyImmediate(tex);
		}

		// ─── Capture: argument validation ───────────────

		[Test]
		public void Capture_EmptyProfileId_Throws()
		{
			var tex = MakeTinyTexture();
			Assert.Throws<ArgumentException>(() => _svc.Capture("", tex, "camping", "forest"));
			UnityEngine.Object.DestroyImmediate(tex);
		}

		[Test]
		public void Capture_NullSnapshot_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => _svc.Capture("p_001", null, "camping", "forest"));
		}

		// ─── FIFO eviction ──────────────────────────────

		[Test]
		public void Capture_OverCap_EvictsOldestEntryAndFile()
		{
			var smallSvc = new PhotoAlbumService(_save, _signals, _photosRoot, maxPhotos: 2);

			var t1 = MakeTinyTexture();
			var t2 = MakeTinyTexture();
			var t3 = MakeTinyTexture();

			var first = smallSvc.Capture("p_001", t1, "camping", "forest");
			var firstPath = smallSvc.GetPhotoPath("p_001", first);
			Assert.IsTrue(File.Exists(firstPath));

			smallSvc.Capture("p_001", t2, "camping", "forest");
			smallSvc.Capture("p_001", t3, "camping", "forest");

			var photos = smallSvc.GetPhotosFor("p_001");
			Assert.AreEqual(2, photos.Count, "Cap of 2 must hold after 3 captures.");
			Assert.IsFalse(photos.Contains(first), "First (oldest) entry must be evicted.");
			Assert.IsFalse(File.Exists(firstPath), "Evicted entry's JPEG must be deleted from disk.");

			UnityEngine.Object.DestroyImmediate(t1);
			UnityEngine.Object.DestroyImmediate(t2);
			UnityEngine.Object.DestroyImmediate(t3);
		}

		[Test]
		public void Capture_OverCap_TolerateMissingFileOnEvict()
		{
			// External file deletion (user, OS cleanup) shouldn't crash the next capture.
			var smallSvc = new PhotoAlbumService(_save, _signals, _photosRoot, maxPhotos: 1);

			var t1 = MakeTinyTexture();
			var t2 = MakeTinyTexture();

			var first = smallSvc.Capture("p_001", t1, "camping", "forest");
			File.Delete(smallSvc.GetPhotoPath("p_001", first));

			Assert.DoesNotThrow(() => smallSvc.Capture("p_001", t2, "camping", "forest"));
			Assert.AreEqual(1, smallSvc.GetPhotosFor("p_001").Count);

			UnityEngine.Object.DestroyImmediate(t1);
			UnityEngine.Object.DestroyImmediate(t2);
		}

		// ─── Profile isolation ──────────────────────────

		[Test]
		public void Capture_TwoProfiles_DoNotShareAlbum()
		{
			var t1 = MakeTinyTexture();
			var t2 = MakeTinyTexture();

			_svc.Capture("p_001", t1, "camping", "forest");
			_svc.Capture("p_002", t2, "camping", "forest");

			Assert.AreEqual(1, _svc.GetPhotosFor("p_001").Count);
			Assert.AreEqual(1, _svc.GetPhotosFor("p_002").Count);
			Assert.AreNotEqual(_svc.GetPhotosFor("p_001")[0].Id, _svc.GetPhotosFor("p_002")[0].Id);

			UnityEngine.Object.DestroyImmediate(t1);
			UnityEngine.Object.DestroyImmediate(t2);
		}

		// ─── Reads ──────────────────────────────────────

		[Test]
		public void GetPhotosFor_UnknownProfile_ReturnsEmpty()
		{
			Assert.IsEmpty(_svc.GetPhotosFor("p_nope"));
		}
	}
}
