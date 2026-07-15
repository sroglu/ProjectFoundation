using System;
using System.Collections.Generic;
using System.IO;
using PFound.HubApp.MiniGame;
using PFound.HubApp.MiniGame.Signals;
using PFound.HubApp.Save;
using PFound.HubApp.Services.Badges;
using PFound.HubApp.Services.PhotoAlbum;
using PFound.HubApp.Services.Profile;
using PFound.HubApp.Services.Stickers;
using PFound.Signaling;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PFound.HubApp.Tests
{
	[TestFixture]
	public class MiniGameHostTests
	{
		// Test double — captures lifecycle calls and exposes the received MiniGameContext for
		// downstream assertions (save scoping, etc.).
		private sealed class StubModule : IMiniGameModule
		{
			public string GameId { get; }
			public int InitCount, PauseCount, ResumeCount, ExitCount;
			public bool ExitThrows;
			public MiniGameContext LastContext;

			public StubModule(string gameId) { GameId = gameId; }

			public void Initialize(MiniGameContext context)
			{
				InitCount++;
				LastContext = context;
			}

			public void Pause() { PauseCount++; }
			public void Resume() { ResumeCount++; }

			public void OnExit()
			{
				ExitCount++;
				if (ExitThrows) throw new InvalidOperationException("simulated OnExit failure");
			}
		}

		// Test double for IMiniGameModuleLoader. Invokes the onLoaded callback synchronously so
		// EditMode tests remain deterministic. Production PrefabMiniGameLoader invokes it sync
		// when the asset is cached (UseAssetDatabase mode) or after a frame (bundle mode).
		private sealed class StubLoader : IMiniGameModuleLoader
		{
			public IMiniGameModule ToReturn;
			public bool ReturnNull;
			public List<string> LoadedAddresses = new List<string>();
			public List<Transform> LoadedParents = new List<Transform>();
			public int UnloadCount;

			public void Load(string moduleAddress, Transform parent, Action<IMiniGameModule, GameObject> onLoaded)
			{
				LoadedAddresses.Add(moduleAddress);
				LoadedParents.Add(parent);
				// Tests that exercise the host's null-module path don't need a backing GameObject.
				// All other paths get a sentinel instance the loader can later "destroy" via Unload.
				GameObject instance = null;
				if (!ReturnNull)
				{
					instance = new GameObject("StubInstance");
					if (parent != null) instance.transform.SetParent(parent, worldPositionStays: false);
				}
				onLoaded?.Invoke(ReturnNull ? null : ToReturn, instance);
			}

			public void Unload(GameObject moduleInstance)
			{
				UnloadCount++;
				if (moduleInstance != null) UnityEngine.Object.DestroyImmediate(moduleInstance);
			}
		}

		private string _saveRoot;
		private SaveService _save;
		private SignalTracker _signals;
		private ProfileService _profile;
		private BadgeService _badges;
		private StickerService _stickers;
		private PhotoAlbumService _photos;
		private StubLoader _loader;
		private MiniGameHost _host;

		private int _startedCount;
		private int _completedCount;
		private int _pausedCount;
		private int _resumedCount;

		private string _activeProfileId;

		[SetUp]
		public void Setup()
		{
			_saveRoot = Path.Combine(Path.GetTempPath(), "MiniGameHostTests_" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_saveRoot);

			_save = new SaveService(_saveRoot);
			_save.Load();

			_signals = new SignalTracker();
			_profile = new ProfileService(_save, _signals);
			_badges = new BadgeService(_save, _signals);
			_stickers = new StickerService(_save, _signals);
			_photos = new PhotoAlbumService(_save, _signals, Path.Combine(_saveRoot, "photos"));

			_loader = new StubLoader();
			_host = new MiniGameHost(_loader, _save, _profile, _badges, _stickers, _photos, _signals);

			// Pre-create + select a profile so Load doesn't trip the "no active profile" guard.
			var entry = _profile.Create("Eli", "4+");
			_profile.SetActive(entry.Id);
			_activeProfileId = entry.Id;
			_signals.EmitQueuedSignals(); // flush ProfileSelected from setup so tests start clean

			_signals.AddListener<MiniGameStartedSignal>(OnStarted);
			_signals.AddListener<MiniGameCompletedSignal>(OnCompleted);
			_signals.AddListener<MiniGamePausedSignal>(OnPaused);
			_signals.AddListener<MiniGameResumedSignal>(OnResumed);

			_startedCount = _completedCount = _pausedCount = _resumedCount = 0;
		}

		[TearDown]
		public void Teardown()
		{
			_signals.RemoveListener<MiniGameStartedSignal>(OnStarted);
			_signals.RemoveListener<MiniGameCompletedSignal>(OnCompleted);
			_signals.RemoveListener<MiniGamePausedSignal>(OnPaused);
			_signals.RemoveListener<MiniGameResumedSignal>(OnResumed);

			if (Directory.Exists(_saveRoot))
				Directory.Delete(_saveRoot, recursive: true);
		}

		private void OnStarted(SignalKey _) => _startedCount++;
		private void OnCompleted(SignalKey _) => _completedCount++;
		private void OnPaused(SignalKey _) => _pausedCount++;
		private void OnResumed(SignalKey _) => _resumedCount++;
		private void Pump() => _signals.EmitQueuedSignals();

		private MiniGameDefinition MakeDef(string gameId, string moduleAddress, bool unlocked = true)
		{
			var def = ScriptableObject.CreateInstance<MiniGameDefinition>();
			def.GameId = gameId;
			def.ModuleAddress = moduleAddress;
			def.Unlocked = unlocked;
			return def;
		}

		// ─── Ctor (boundary validation) ──────────────────

		[Test]
		public void Ctor_NullLoader_Throws()
		{
			Assert.Throws<ArgumentNullException>(() =>
				new MiniGameHost(null, _save, _profile, _badges, _stickers, _photos, _signals));
		}

		[Test]
		public void Ctor_NullSave_Throws()
		{
			Assert.Throws<ArgumentNullException>(() =>
				new MiniGameHost(_loader, null, _profile, _badges, _stickers, _photos, _signals));
		}

		[Test]
		public void Ctor_NullProfile_Throws()
		{
			Assert.Throws<ArgumentNullException>(() =>
				new MiniGameHost(_loader, _save, null, _badges, _stickers, _photos, _signals));
		}

		[Test]
		public void Ctor_NullSignals_Throws()
		{
			Assert.Throws<ArgumentNullException>(() =>
				new MiniGameHost(_loader, _save, _profile, _badges, _stickers, _photos, null));
		}

		// ─── Initial state ──────────────────────────────

		[Test]
		public void Initial_NotLoaded()
		{
			Assert.IsFalse(_host.IsLoaded);
			Assert.IsFalse(_host.IsLoading);
			Assert.IsNull(_host.ActiveModule);
			Assert.IsNull(_host.ActiveDefinition);
		}

		// ─── Load: argument + state validation ──────────

		[Test]
		public void Load_NullDefinition_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => _host.Load(null));
		}

		[Test]
		public void Load_EmptyGameId_Throws()
		{
			var def = MakeDef("", "tossy_module");
			Assert.Throws<ArgumentException>(() => _host.Load(def));
		}

		[Test]
		public void Load_EmptyModuleAddress_Throws()
		{
			var def = MakeDef("tossy", "");
			Assert.Throws<ArgumentException>(() => _host.Load(def));
		}

		[Test]
		public void Load_LockedDefinition_Throws()
		{
			var def = MakeDef("tossy", "tossy_module", unlocked: false);
			Assert.Throws<InvalidOperationException>(() => _host.Load(def));
		}

		[Test]
		public void Load_NoActiveProfile_Throws()
		{
			_profile.ClearActive();
			Pump();

			_loader.ToReturn = new StubModule("tossy");
			var def = MakeDef("tossy", "tossy_module");
			Assert.Throws<InvalidOperationException>(() => _host.Load(def));
		}

		[Test]
		public void Load_LoaderReturnsNull_Throws()
		{
			_loader.ReturnNull = true;
			var def = MakeDef("tossy", "tossy_module");
			Assert.Throws<InvalidOperationException>(() => _host.Load(def));
		}

		[Test]
		public void Load_GameIdMismatch_Throws()
		{
			_loader.ToReturn = new StubModule("camping");
			var def = MakeDef("tossy", "tossy_module");
			Assert.Throws<InvalidOperationException>(() => _host.Load(def));
		}

		[Test]
		public void Load_DoubleLoad_Throws()
		{
			_loader.ToReturn = new StubModule("tossy");
			_host.Load(MakeDef("tossy", "tossy_module"));

			_loader.ToReturn = new StubModule("camping");
			Assert.Throws<InvalidOperationException>(() => _host.Load(MakeDef("camping", "camping_module")));
		}

		// ─── Load: happy path ──────────────────────────

		[Test]
		public void Load_HappyPath_InitializesModuleAndFiresStartedSignal()
		{
			var module = new StubModule("tossy");
			_loader.ToReturn = module;
			var def = MakeDef("tossy", "tossy_module");

			_host.Load(def);
			Pump();

			Assert.AreEqual(1, module.InitCount);
			Assert.AreSame(module, _host.ActiveModule);
			Assert.AreSame(def, _host.ActiveDefinition);
			Assert.IsTrue(_host.IsLoaded);
			Assert.AreEqual(1, _startedCount);
			CollectionAssert.AreEqual(new[] { "tossy_module" }, _loader.LoadedAddresses);
		}

		[Test]
		public void Load_ContextReceivedByModule_HasCorrectServices()
		{
			var module = new StubModule("tossy");
			_loader.ToReturn = module;

			_host.Load(MakeDef("tossy", "tossy_module"));

			Assert.NotNull(module.LastContext.Save);
			Assert.AreSame(_profile, module.LastContext.Profile);
			Assert.AreSame(_badges, module.LastContext.Badges);
			Assert.AreSame(_stickers, module.LastContext.Stickers);
			Assert.AreSame(_photos, module.LastContext.PhotoAlbum);
			Assert.NotNull(module.LastContext.RequestExit);
		}

		[Test]
		public void Load_SaveScopedToActiveProfileAndGameId()
		{
			var module = new StubModule("tossy");
			_loader.ToReturn = module;
			_host.Load(MakeDef("tossy", "tossy_module"));

			module.LastContext.Save.Set("progress", 42);

			// Round-tripped via ScopedSaveService → games.{profileId}.tossy.progress.
			var bag = _save.Schema.Games[_activeProfileId].PerGameState["tossy"];
			Assert.AreEqual(42, (int)bag["progress"]);
		}

		[Test]
		public void Load_OnReadyCallbackFires()
		{
			var module = new StubModule("tossy");
			_loader.ToReturn = module;
			var readyCalled = false;

			_host.Load(MakeDef("tossy", "tossy_module"), onReady: () => readyCalled = true);

			Assert.IsTrue(readyCalled);
			Assert.IsTrue(_host.IsLoaded);
		}

		// ─── Pause / Resume ─────────────────────────────

		[Test]
		public void Pause_OnLoadedModule_CallsModuleAndFiresSignal()
		{
			var module = new StubModule("tossy");
			_loader.ToReturn = module;
			_host.Load(MakeDef("tossy", "tossy_module"));
			Pump();

			_host.Pause();
			Pump();

			Assert.AreEqual(1, module.PauseCount);
			Assert.AreEqual(1, _pausedCount);
		}

		[Test]
		public void Resume_OnLoadedModule_CallsModuleAndFiresSignal()
		{
			var module = new StubModule("tossy");
			_loader.ToReturn = module;
			_host.Load(MakeDef("tossy", "tossy_module"));
			_host.Pause();
			Pump();

			_host.Resume();
			Pump();

			Assert.AreEqual(1, module.ResumeCount);
			Assert.AreEqual(1, _resumedCount);
		}

		[Test]
		public void Pause_WhenNotLoaded_NoOp_NoSignal()
		{
			_host.Pause();
			Pump();
			Assert.AreEqual(0, _pausedCount);
		}

		[Test]
		public void Resume_WhenNotLoaded_NoOp_NoSignal()
		{
			_host.Resume();
			Pump();
			Assert.AreEqual(0, _resumedCount);
		}

		// ─── RequestExit ───────────────────────────────

		[Test]
		public void RequestExit_TearsDownAndFiresCompletedSignal()
		{
			var module = new StubModule("tossy");
			_loader.ToReturn = module;
			_host.Load(MakeDef("tossy", "tossy_module"));
			Pump();

			_host.RequestExit();
			Pump();

			Assert.AreEqual(1, _completedCount);
			Assert.AreEqual(1, module.ExitCount);
			Assert.IsNull(_host.ActiveModule);
			Assert.IsNull(_host.ActiveDefinition);
			Assert.IsFalse(_host.IsLoaded);
			Assert.AreEqual(1, _loader.UnloadCount);
		}

		[Test]
		public void RequestExit_QueuesCompletedSignalBeforeOnExit()
		{
			var module = new StubModule("tossy");
			_loader.ToReturn = module;
			_host.Load(MakeDef("tossy", "tossy_module"));
			Pump();

			var sb = new System.Text.StringBuilder();
			Action<SignalKey> listener = _ => sb.Append("S");
			_signals.AddListener<MiniGameCompletedSignal>(listener);
			try
			{
				module.ExitThrows = false;
				_host.RequestExit();
				Pump();
				sb.Append("E");
			}
			finally { _signals.RemoveListener<MiniGameCompletedSignal>(listener); }

			Assert.AreEqual("SE", sb.ToString());
		}

		[Test]
		public void RequestExit_WhenNotLoaded_NoOp_NoSignal()
		{
			_host.RequestExit();
			Pump();
			Assert.AreEqual(0, _completedCount);
		}

		[Test]
		public void RequestExit_Twice_SecondIsNoOp()
		{
			var module = new StubModule("tossy");
			_loader.ToReturn = module;
			_host.Load(MakeDef("tossy", "tossy_module"));

			_host.RequestExit();
			_host.RequestExit();
			Pump();

			Assert.AreEqual(1, _completedCount);
			Assert.AreEqual(1, module.ExitCount);
			Assert.AreEqual(1, _loader.UnloadCount);
		}

		[Test]
		public void RequestExit_FromContextCallback_TriggersFullTeardown()
		{
			var module = new StubModule("tossy");
			_loader.ToReturn = module;
			_host.Load(MakeDef("tossy", "tossy_module"));

			module.LastContext.RequestExit();
			Pump();

			Assert.AreEqual(1, module.ExitCount);
			Assert.IsFalse(_host.IsLoaded);
			Assert.AreEqual(1, _completedCount);
		}

		[Test]
		public void RequestExit_OnExitThrows_StillCompletesTeardown()
		{
			var module = new StubModule("tossy") { ExitThrows = true };
			_loader.ToReturn = module;
			_host.Load(MakeDef("tossy", "tossy_module"));

			LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*OnExit threw.*"));

			Assert.DoesNotThrow(() => _host.RequestExit());
			Assert.IsFalse(_host.IsLoaded);
			Assert.AreEqual(1, _loader.UnloadCount, "Unload still happens despite OnExit failure.");
		}

		// ─── Reload after exit ─────────────────────────

		[Test]
		public void Load_AfterExit_AllowsAnotherMiniGame()
		{
			var first = new StubModule("tossy");
			_loader.ToReturn = first;
			_host.Load(MakeDef("tossy", "tossy_module"));
			_host.RequestExit();
			Pump();

			var second = new StubModule("camping");
			_loader.ToReturn = second;
			Assert.DoesNotThrow(() => _host.Load(MakeDef("camping", "camping_module")));

			Assert.AreSame(second, _host.ActiveModule);
			Assert.AreEqual(1, second.InitCount);
		}
	}
}
