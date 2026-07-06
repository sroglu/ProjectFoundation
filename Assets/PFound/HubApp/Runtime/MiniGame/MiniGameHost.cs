using System;
using Cysharp.Threading.Tasks;
using PFound.HubApp.MiniGame.Signals;
using PFound.HubApp.Save;
using PFound.HubApp.Services.Badges;
using PFound.HubApp.Services.PhotoAlbum;
using PFound.HubApp.Services.Profile;
using PFound.HubApp.Services.Stickers;
using PFound.Signaling;
using UnityEngine;

namespace PFound.HubApp.MiniGame
{
	/// <summary>
	/// Orchestrates the full <see cref="IMiniGameModule"/> lifecycle (HubApp_TDD §7.2):
	/// load module prefab via AssetSystem → build <see cref="MiniGameContext"/> → <c>Initialize</c>
	/// → fire <c>MiniGameStartedSignal</c> → run → on <c>RequestExit</c> fire
	/// <c>MiniGameCompletedSignal</c> → <c>OnExit</c> → destroy module instance → memory sweep.
	/// </summary>
	/// <remarks>
	/// Single-occupant: only one mini-game may be loaded at a time. The host throws on a second
	/// <see cref="Load"/> while one is already active or loading — preventing it via "queue"
	/// semantics would hide a state bug in whichever caller forgot to exit first.
	/// Memory sweep: <see cref="UnityEngine.Resources.UnloadUnusedAssets"/> + <see cref="GC.Collect"/>
	/// after every unload, per HubApp_TDD §7.2. The cost (~30-100 ms) is acceptable on the way
	/// back to Home where a transition animation hides it.
	/// </remarks>
	public class MiniGameHost
	{
		private readonly IMiniGameModuleLoader _loader;
		private readonly ISaveService _save;
		private readonly IProfileService _profile;
		private readonly IBadgeService _badges;
		private readonly IStickerService _stickers;
		private readonly IPhotoAlbumService _photoAlbum;
		private readonly SignalTracker _signals;

		// Active state. Null when no mini-game is loaded.
		private IMiniGameModule _activeModule;
		private GameObject _activeInstance;
		private MiniGameDefinition _activeDefinition;
		private bool _isLoading;

		public MiniGameHost(
			IMiniGameModuleLoader loader,
			ISaveService save,
			IProfileService profile,
			IBadgeService badges,
			IStickerService stickers,
			IPhotoAlbumService photoAlbum,
			SignalTracker signals)
		{
			if (loader == null) throw new ArgumentNullException(nameof(loader));
			if (save == null) throw new ArgumentNullException(nameof(save));
			if (profile == null) throw new ArgumentNullException(nameof(profile));
			if (badges == null) throw new ArgumentNullException(nameof(badges));
			if (stickers == null) throw new ArgumentNullException(nameof(stickers));
			if (photoAlbum == null) throw new ArgumentNullException(nameof(photoAlbum));
			if (signals == null) throw new ArgumentNullException(nameof(signals));

			_loader = loader;
			_save = save;
			_profile = profile;
			_badges = badges;
			_stickers = stickers;
			_photoAlbum = photoAlbum;
			_signals = signals;
		}

		/// <summary>The mini-game currently loaded, or null if none.</summary>
		public IMiniGameModule ActiveModule => _activeModule;

		/// <summary>The definition currently loaded, or null if none.</summary>
		public MiniGameDefinition ActiveDefinition => _activeDefinition;

		/// <summary>True when a mini-game is fully loaded and initialized.</summary>
		public bool IsLoaded => _activeModule != null;

		/// <summary>True while a scene load is in flight (between Load call and callback).</summary>
		public bool IsLoading => _isLoading;

		/// <summary>
		/// Loads the mini-game described by <paramref name="definition"/>, builds the
		/// <see cref="MiniGameContext"/> bound to the active profile, and fires
		/// <see cref="MiniGameStartedSignal"/>. The optional <paramref name="onReady"/> callback
		/// fires after the module is initialized — use it when you need to know exactly when the
		/// mini-game is playable (e.g. to dismiss a loading splash).
		///
		/// <paramref name="moduleAddressOverride"/> allows the caller to load a different prefab
		/// than <c>definition.ModuleAddress</c> while keeping the same definition (for save scope,
		/// gameId, locked state). Used by hosts that split a mini-game into multiple phase prefabs
		/// (e.g. <c>&lt;gameId&gt;_lobby</c> + <c>&lt;gameId&gt;_game</c>) and pick the address
		/// based on which scene is loading. When null, falls back to <c>definition.ModuleAddress</c>.
		///
		/// <paramref name="parent"/>, when non-null, parents the spawned module instance under
		/// the given transform — typically a per-scene <c>MiniGameContentRoot</c> so the module
		/// lives inside the active scene's hierarchy instead of at the root. When null the
		/// instance lands at the active scene's root.
		///
		/// <paramref name="sceneContext"/>, when non-null, is surfaced on
		/// <see cref="MiniGameContext.Scene"/> so the module can reach scene-level refs (e.g. the
		/// scene camera) it cannot serialize itself. Typically the <see cref="MiniGameSceneContext"/>
		/// component on the same <c>MiniGameContentRoot</c>.
		/// </summary>
		/// <remarks>
		/// The loader's <c>onLoaded</c> callback may fire synchronously (AssetSystem editor cache
		/// path or a test stub) or after a frame (bundle path). The host validates all
		/// preconditions synchronously (null checks, lock state, profile) but module discovery +
		/// initialization happen in the loader's callback.
		/// </remarks>
		public void Load(MiniGameDefinition definition, string moduleAddressOverride = null, Transform parent = null, Action onReady = null, MiniGameSceneContext sceneContext = null)
		{
			if (definition == null) throw new ArgumentNullException(nameof(definition));
			if (string.IsNullOrEmpty(definition.GameId))
				throw new ArgumentException("definition.GameId must not be empty.", nameof(definition));

			var moduleAddress = !string.IsNullOrEmpty(moduleAddressOverride)
				? moduleAddressOverride
				: definition.ModuleAddress;
			if (string.IsNullOrEmpty(moduleAddress))
				throw new ArgumentException(
					"Either moduleAddressOverride or definition.ModuleAddress must be non-empty.",
					nameof(definition));

			if (!definition.Unlocked)
				throw new InvalidOperationException(
					$"Mini-game '{definition.GameId}' is locked. Unlock via IAP/content pack before loading.");

			if (_activeModule != null || _isLoading)
				throw new InvalidOperationException(
					$"A mini-game ('{_activeDefinition?.GameId}') is already loaded or loading. Call RequestExit before loading another.");

			var profileId = _profile.ActiveProfileId;
			if (string.IsNullOrEmpty(profileId))
				throw new InvalidOperationException(
					"No active profile. Mini-games can only be loaded after a profile is selected (Shell guarantees this — host failing here means routing is broken).");

			_isLoading = true;
			_activeDefinition = definition;

			_loader.Load(moduleAddress, parent, (module, instance) =>
			{
				_isLoading = false;

				if (module == null)
					throw new InvalidOperationException(
						$"Loader returned null IMiniGameModule for moduleAddress '{moduleAddress}'. " +
						"The prefab must contain exactly one MonoBehaviour implementing IMiniGameModule.");

				if (!string.Equals(module.GameId, definition.GameId, StringComparison.Ordinal))
					throw new InvalidOperationException(
						$"GameId mismatch: definition '{definition.GameId}' vs module '{module.GameId}'. " +
						"Definition.GameId must match IMiniGameModule.GameId — fix the SO or the module.");

				var scopedSave = new ScopedSaveService(_save, profileId, definition.GameId);
				var context = new MiniGameContext(
					save: scopedSave,
					profile: _profile,
					badges: _badges,
					stickers: _stickers,
					photoAlbum: _photoAlbum,
					requestExit: RequestExit,
					scene: sceneContext);

				_activeModule = module;
				_activeInstance = instance;
				module.Initialize(context);
				_signals.Queue<MiniGameStartedSignal>(causer: null);

				// A module that loads content asynchronously in Initialize (e.g. an AssetSystem env
				// prefab) keeps the loading cover up until its content is ready — otherwise onReady
				// dismisses the cover while the heavy load is still running, so the load + first-render
				// shader compilation hitch happen on-screen during the fade-out.
				if (module is IMiniGameModuleAsyncReady asyncReady)
					WaitReadyThenInvoke(asyncReady, onReady).Forget();
				else
					onReady?.Invoke();
			});
		}

		static async UniTaskVoid WaitReadyThenInvoke(IMiniGameModuleAsyncReady module, Action onReady)
		{
			await module.WaitUntilReadyAsync(UnityEngine.Application.exitCancellationToken);
			onReady?.Invoke();
		}

		/// <summary>
		/// Suspends the active mini-game (app backgrounded, parent gate open, etc.). Calls
		/// <see cref="IMiniGameModule.Pause"/> and queues <see cref="MiniGamePausedSignal"/>.
		/// No-op if nothing is loaded — pause/resume on an empty host is a benign event-routing
		/// case (Shell may broadcast on app focus without checking).
		/// </summary>
		public void Pause()
		{
			if (_activeModule == null) return;
			_activeModule.Pause();
			_signals.Queue<MiniGamePausedSignal>(causer: null);
		}

		public void Resume()
		{
			if (_activeModule == null) return;
			_activeModule.Resume();
			_signals.Queue<MiniGameResumedSignal>(causer: null);
		}

		/// <summary>
		/// Tears down the active mini-game. Invoked by the module via the context's
		/// <c>RequestExit</c> callback. Idempotent — extra calls after the first are no-ops so
		/// a module that fires RequestExit and then errors before its own cleanup completes
		/// won't drag the host into double-unload territory.
		/// </summary>
		public void RequestExit()
		{
			if (_activeModule == null) return;
			_signals.Queue<MiniGameCompletedSignal>(causer: null);
			TearDownActive();
		}

		/// <summary>
		/// Tears down the active mini-game without firing <see cref="MiniGameCompletedSignal"/>.
		/// Used by hosts that orchestrate phase-to-phase scene transitions (e.g. lobby → game)
		/// where the lobby module's prefab is destroyed by the scene unload but the broader
		/// "mini-game session" isn't done — only the lobby phase is. Firing the completed signal
		/// here would route back to Home and abort the transition.
		///
		/// Idempotent like <see cref="RequestExit"/>.
		/// </summary>
		public void UnloadActive()
		{
			if (_activeModule == null) return;
			TearDownActive();
		}

		void TearDownActive()
		{
			var module = _activeModule;
			var instance = _activeInstance;
			var definition = _activeDefinition;

			try
			{
				module.OnExit();
			}
			catch (Exception e)
			{
				Debug.LogError($"[MiniGameHost] {definition?.GameId} OnExit threw: {e}");
			}

			_activeModule = null;
			_activeInstance = null;
			_activeDefinition = null;

			_loader.Unload(instance);

			Resources.UnloadUnusedAssets();
			GC.Collect();
		}
	}
}
