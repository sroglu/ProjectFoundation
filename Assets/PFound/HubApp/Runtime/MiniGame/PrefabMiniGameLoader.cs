using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using PFound.ContentDelivery;
using Unity.Collections;
using UnityEngine;

namespace PFound.HubApp.MiniGame
{
	/// <summary>
	/// Production <see cref="IMiniGameModuleLoader"/> over the
	/// <c>AssetSystem</c> bundle pipeline. <see cref="Load"/> warms the module
	/// prefab via <see cref="AssetLoader.LoadAssetAsync{T}"/> (UseAssetDatabase
	/// reads from the editor cache, bundle mode from the open catalog) and then
	/// instantiates it WITHOUT blocking the main thread (Unity 6
	/// <see cref="UnityEngine.Object.InstantiateAsync{T}(T,Transform)"/>), so a
	/// loading cover's progress bar keeps animating while a large prefab is built.
	/// It walks the instance for the lone <see cref="IMiniGameModule"/> component.
	/// <see cref="Unload"/> destroys the instance and drops the warm asset ref.
	/// </summary>
	public class PrefabMiniGameLoader : IMiniGameModuleLoader, IDisposable
	{
		readonly AssetLoader _loader;
		// instance → address, so Unload can release the matching warm LoadAssetAsync ref.
		readonly Dictionary<GameObject, string> _addressByInstance = new();

		public PrefabMiniGameLoader()
		{
			_loader = new AssetLoader(new FixedString64Bytes("MiniGameModuleLoader"));
		}

		public void Load(string moduleAddress, Transform parent, Action<IMiniGameModule, GameObject> onLoaded)
		{
			if (string.IsNullOrEmpty(moduleAddress))
				throw new ArgumentException("moduleAddress must not be empty.", nameof(moduleAddress));
			if (onLoaded == null)
				throw new ArgumentNullException(nameof(onLoaded));

			// Fully async: a big mini-game bundle (decompress + asset load) AND a monolithic prefab's
			// synchronous Instantiate each blocked the main thread for seconds — the loading bar froze.
			// Warm the asset off-thread first, then InstantiateAsync spreads the build across frames so
			// the cover keeps progressing. The host's onLoaded contract already allows a deferred callback.
			LoadInternalAsync(moduleAddress, parent, onLoaded).Forget();
		}

		async UniTaskVoid LoadInternalAsync(
			string moduleAddress, Transform parent, Action<IMiniGameModule, GameObject> onLoaded)
		{
			var handle = _loader.LoadAssetAsync<GameObject>(moduleAddress);
			await handle.Task;
			var prefab = handle.Result;
			if (prefab == null)
				throw new InvalidOperationException(
					$"AssetSystem returned null for moduleAddress '{moduleAddress}'. " +
					"Catalog missing the address, group not registered on Assets.asset, or bundle mode without an initialized catalog.");

			var op = parent != null
				? UnityEngine.Object.InstantiateAsync(prefab, parent)
				: UnityEngine.Object.InstantiateAsync(prefab);
			await op.ToUniTask();
			var instance = op.Result[0];
			_addressByInstance.Add(instance, moduleAddress);

			var module = instance.GetComponentsInChildren<MonoBehaviour>(includeInactive: true)
			                     .OfType<IMiniGameModule>()
			                     .FirstOrDefault();

			onLoaded(module, instance);
		}

		public void Unload(GameObject moduleInstance)
		{
			if (moduleInstance == null) return;
			// InstantiateAsync builds a plain instance (not tracked by AssetManager's instance map), so
			// destroy it directly and drop the warm asset ref ourselves. Fall back to the loader's tracked
			// Destroy for anything created the old way.
			if (_addressByInstance.Remove(moduleInstance, out var address))
			{
				UnityEngine.Object.Destroy(moduleInstance);
				_loader.UnloadAsset(address);
			}
			else
			{
				_loader.Destroy(moduleInstance);
			}
		}

		public void Dispose() => _loader.Dispose();
	}
}
