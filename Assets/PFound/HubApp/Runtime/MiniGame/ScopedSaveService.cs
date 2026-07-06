using System;
using System.Collections.Generic;
using PFound.HubApp.Save;
using Newtonsoft.Json.Linq;

namespace PFound.HubApp.MiniGame
{
	/// <summary>
	/// Wraps an <see cref="ISaveService"/> and routes every read/write into the bag at
	/// <c>Schema.Games[profileId].PerGameState[gameId]</c>. Set auto-creates the per-profile
	/// and per-game subtree on first write; Get/Has/Remove never auto-create — they fail
	/// loud on missing keys so mini-game state-machine bugs surface immediately.
	/// </summary>
	public class ScopedSaveService : IScopedSaveService
	{
		private readonly ISaveService _inner;
		private readonly string _profileId;
		private readonly string _gameId;

		public ScopedSaveService(ISaveService inner, string profileId, string gameId)
		{
			if (inner == null)
				throw new ArgumentNullException(nameof(inner));
			if (string.IsNullOrEmpty(profileId))
				throw new ArgumentException("profileId must not be empty.", nameof(profileId));
			if (string.IsNullOrEmpty(gameId))
				throw new ArgumentException("gameId must not be empty.", nameof(gameId));

			_inner = inner;
			_profileId = profileId;
			_gameId = gameId;
		}

		public bool Has(string key)
		{
			if (string.IsNullOrEmpty(key))
				throw new ArgumentException("key must not be empty.", nameof(key));

			// Has() is the explicit "is there anything?" probe — false is a valid answer for
			// "no profile subtree yet" or "no game subtree yet". This is NOT a silent fallback
			// for missing data; it's the documented contract callers use to gate Get<T>().
			if (!_inner.Schema.Games.TryGetValue(_profileId, out var profileGames))
				return false;
			if (!profileGames.PerGameState.TryGetValue(_gameId, out var bag))
				return false;
			return bag[key] != null;
		}

		public T Get<T>(string key)
		{
			if (string.IsNullOrEmpty(key))
				throw new ArgumentException("key must not be empty.", nameof(key));

			// Dictionary indexers throw KeyNotFoundException on missing profile/game subtree —
			// that's exactly the fail-fast outcome we want; no extra null-checks needed.
			var bag = _inner.Schema.Games[_profileId].PerGameState[_gameId];

			// JObject indexer returns null (not throws) on missing key — convert to a
			// KeyNotFoundException so the caller sees the same exception type as a missing
			// dictionary key, with the full namespaced path in the message.
			var token = bag[key];
			if (token == null)
				throw new KeyNotFoundException(
					$"Key '{key}' not found in games.{_profileId}.{_gameId}. Use Has() to gate.");

			return token.ToObject<T>();
		}

		public void Set<T>(string key, T value)
		{
			if (string.IsNullOrEmpty(key))
				throw new ArgumentException("key must not be empty.", nameof(key));

			GetOrCreateBag()[key] = JToken.FromObject(value);
		}

		public void Remove(string key)
		{
			if (string.IsNullOrEmpty(key))
				throw new ArgumentException("key must not be empty.", nameof(key));

			var bag = _inner.Schema.Games[_profileId].PerGameState[_gameId];
			if (bag[key] == null)
				throw new KeyNotFoundException(
					$"Cannot remove '{key}' from games.{_profileId}.{_gameId} — key does not exist.");
			bag.Remove(key);
		}

		private JObject GetOrCreateBag()
		{
			if (!_inner.Schema.Games.TryGetValue(_profileId, out var profileGames))
			{
				profileGames = new GamesProfileData();
				_inner.Schema.Games[_profileId] = profileGames;
			}

			if (!profileGames.PerGameState.TryGetValue(_gameId, out var bag))
			{
				bag = new JObject();
				profileGames.PerGameState[_gameId] = bag;
			}

			return bag;
		}
	}
}
