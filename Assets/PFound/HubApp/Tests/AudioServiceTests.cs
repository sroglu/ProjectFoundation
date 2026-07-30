using System;
using System.IO;
using System.Reflection;
using PFound.HubApp.Save;
using PFound.HubApp.Services.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace PFound.HubApp.Tests
{
	[TestFixture]
	public class AudioServiceTests
	{
		private string _saveRoot;
		private SaveService _save;
		private AudioMixer _mixer;
		private AudioMixerSnapshot _defaultSnapshot;
		private AudioMixerSnapshot _duckingSnapshot;
		private AudioService _service;

		private const string TestMixerPath = "Assets/AudioServiceTest_Temp.mixer";

		[SetUp]
		public void Setup()
		{
			_saveRoot = Path.Combine(Path.GetTempPath(), "AudioServiceTests_" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_saveRoot);

			_save = new SaveService(_saveRoot);
			_save.Load();

			CreateTestMixer();
			_service = new AudioService(_save, _mixer, _defaultSnapshot, _duckingSnapshot);
		}

		[TearDown]
		public void Teardown()
		{
			if (_mixer != null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(_mixer)))
				AssetDatabase.DeleteAsset(TestMixerPath);

			if (Directory.Exists(_saveRoot))
				Directory.Delete(_saveRoot, recursive: true);

			AudioListener.volume = 1f;
		}

		private void CreateTestMixer()
		{
			// AudioMixerController is internal in Unity 6 — access via reflection.
			var controllerType = Type.GetType("UnityEditor.Audio.AudioMixerController, UnityEditor");
			var createMethod = controllerType.GetMethod("CreateMixerControllerAtPath",
				BindingFlags.Public | BindingFlags.Static);
			var mixerObj = createMethod.Invoke(null, new object[] { TestMixerPath });

			_mixer = mixerObj as AudioMixer;

			// Get snapshots (default is auto-created)
			var snapshotsProp = controllerType.GetProperty("snapshots",
				BindingFlags.Public | BindingFlags.Instance);
			var snapshots = snapshotsProp.GetValue(mixerObj) as AudioMixerSnapshot[];
			_defaultSnapshot = snapshots[0];

			// Clone a ducking snapshot
			var cloneMethod = controllerType.GetMethod("CloneNewSnapshotFromTarget",
				BindingFlags.Public | BindingFlags.Instance);
			cloneMethod.Invoke(mixerObj, new object[] { true });

			snapshots = snapshotsProp.GetValue(mixerObj) as AudioMixerSnapshot[];
			_duckingSnapshot = snapshots[1];
			_duckingSnapshot.name = "VoiceDucking";
		}

		// ─── Ctor validation ─────────────────────────────

		[Test]
		public void Ctor_NullSave_Throws()
		{
			Assert.Throws<ArgumentNullException>(() =>
				new AudioService(null, _mixer, _defaultSnapshot, _duckingSnapshot));
		}

		[Test]
		public void Ctor_NullMixer_Throws()
		{
			Assert.Throws<ArgumentNullException>(() =>
				new AudioService(_save, null, _defaultSnapshot, _duckingSnapshot));
		}

		[Test]
		public void Ctor_NullDefaultSnapshot_Throws()
		{
			Assert.Throws<ArgumentNullException>(() =>
				new AudioService(_save, _mixer, null, _duckingSnapshot));
		}

		[Test]
		public void Ctor_NullDuckingSnapshot_Throws()
		{
			Assert.Throws<ArgumentNullException>(() =>
				new AudioService(_save, _mixer, _defaultSnapshot, null));
		}

		// ─── Initial state ──────────────────────────────

		[Test]
		public void Initial_NotDucking()
		{
			Assert.IsFalse(_service.IsDucking);
		}

		[Test]
		public void Initial_NotMuted()
		{
			Assert.IsFalse(_service.IsMuted);
		}

		// ─── SetVolume / GetVolume ───────────────────────

		[Test]
		public void SetVolume_GetVolume_Roundtrips()
		{
			_service.SetVolume(AudioBus.Music, 0.6f);
			Assert.AreEqual(0.6f, _service.GetVolume(AudioBus.Music), 0.02f);
		}

		[Test]
		public void SetVolume_ClampsAboveOne()
		{
			_service.SetVolume(AudioBus.SFX, 1.5f);
			Assert.AreEqual(1f, _service.GetVolume(AudioBus.SFX), 0.01f);
		}

		[Test]
		public void SetVolume_ClampsBelowZero()
		{
			_service.SetVolume(AudioBus.Voice, -0.5f);
			Assert.AreEqual(0f, _service.GetVolume(AudioBus.Voice), 0.01f);
		}

		[Test]
		public void SetVolume_Zero_ReturnsSilence()
		{
			_service.SetVolume(AudioBus.Music, 0f);
			Assert.AreEqual(0f, _service.GetVolume(AudioBus.Music), 0.01f);
		}

		[Test]
		public void SetVolume_PersistsMusic()
		{
			_service.SetVolume(AudioBus.Music, 0.7f);
			Assert.AreEqual(0.7f, _save.Schema.Settings.MusicVolume, 0.01f);
		}

		[Test]
		public void SetVolume_PersistsSfx()
		{
			_service.SetVolume(AudioBus.SFX, 0.4f);
			Assert.AreEqual(0.4f, _save.Schema.Settings.SfxVolume, 0.01f);
		}

		[Test]
		public void SetVolume_PersistsVoice()
		{
			_service.SetVolume(AudioBus.Voice, 0.9f);
			Assert.AreEqual(0.9f, _save.Schema.Settings.VoiceVolume, 0.01f);
		}

		[Test]
		public void SetVolume_Master_DoesNotPersist()
		{
			var musicBefore = _save.Schema.Settings.MusicVolume;
			_service.SetVolume(AudioBus.Master, 0.5f);
			Assert.AreEqual(musicBefore, _save.Schema.Settings.MusicVolume, 0.01f);
		}

		[Test]
		public void SetVolume_UI_DoesNotPersist()
		{
			_service.SetVolume(AudioBus.UI, 0.5f);
			Assert.AreEqual(0.5f, _service.GetVolume(AudioBus.UI), 0.02f);
		}

		// ─── Ducking ────────────────────────────────────

		[Test]
		public void StartDucking_SetsIsDucking()
		{
			_service.StartDucking();
			Assert.IsTrue(_service.IsDucking);
		}

		[Test]
		public void StopDucking_ClearsIsDucking()
		{
			_service.StartDucking();
			_service.StopDucking();
			Assert.IsFalse(_service.IsDucking);
		}

		[Test]
		public void StartDucking_Idempotent()
		{
			_service.StartDucking();
			Assert.DoesNotThrow(() => _service.StartDucking());
			Assert.IsTrue(_service.IsDucking);
		}

		[Test]
		public void StopDucking_WhenNotDucking_Idempotent()
		{
			Assert.DoesNotThrow(() => _service.StopDucking());
			Assert.IsFalse(_service.IsDucking);
		}

		// ─── Mute ───────────────────────────────────────

		[Test]
		public void SetMuted_True_SetsAudioListenerToZero()
		{
			_service.SetMuted(true);
			Assert.IsTrue(_service.IsMuted);
			Assert.AreEqual(0f, AudioListener.volume, 0.001f);
		}

		[Test]
		public void SetMuted_False_RestoresAudioListener()
		{
			_service.SetMuted(true);
			_service.SetMuted(false);
			Assert.IsFalse(_service.IsMuted);
			Assert.AreEqual(1f, AudioListener.volume, 0.001f);
		}

		[Test]
		public void SetMuted_Idempotent()
		{
			_service.SetMuted(true);
			_service.SetMuted(true);
			Assert.IsTrue(_service.IsMuted);
			Assert.AreEqual(0f, AudioListener.volume, 0.001f);
		}
	}
}
