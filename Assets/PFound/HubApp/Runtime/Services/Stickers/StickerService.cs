using System;
using System.Collections.Generic;
using PFound.HubApp.MiniGame.Signals;
using PFound.HubApp.Save;
using PFound.Signaling;

namespace PFound.HubApp.Services.Stickers
{
	/// <summary>
	/// Concrete <see cref="IStickerService"/>. Same shape as <see cref="Badges.BadgeService"/>;
	/// kept as separate types (rather than a shared generic) so save-schema field references
	/// (Badges vs Stickers) stay strongly typed and easy to grep.
	/// </summary>
	public class StickerService : IStickerService
	{
		private static readonly IReadOnlyList<EarnedSticker> EmptyStickers = Array.Empty<EarnedSticker>();

		private readonly ISaveService _save;
		private readonly SignalTracker _signals;

		public StickerService(ISaveService save, SignalTracker signals)
		{
			if (save == null) throw new ArgumentNullException(nameof(save));
			if (signals == null) throw new ArgumentNullException(nameof(signals));

			_save = save;
			_signals = signals;
		}

		public bool Award(string profileId, string stickerId, string sourceGame)
		{
			if (string.IsNullOrEmpty(profileId))
				throw new ArgumentException("profileId must not be empty.", nameof(profileId));
			if (string.IsNullOrEmpty(stickerId))
				throw new ArgumentException("stickerId must not be empty.", nameof(stickerId));

			var bag = GetOrCreateShared(profileId).Stickers;
			if (ContainsSticker(bag, stickerId))
				return false;

			bag.Add(new EarnedSticker
			{
				Id = stickerId,
				EarnedAt = DateTime.UtcNow,
				SourceGame = sourceGame
			});

			_signals.Queue<StickerPersistedSignal>(causer: null);
			return true;
		}

		public bool HasSticker(string profileId, string stickerId)
		{
			if (string.IsNullOrEmpty(profileId))
				throw new ArgumentException("profileId must not be empty.", nameof(profileId));
			if (string.IsNullOrEmpty(stickerId))
				throw new ArgumentException("stickerId must not be empty.", nameof(stickerId));

			return _save.Schema.Shared.TryGetValue(profileId, out var shared)
			    && ContainsSticker(shared.Stickers, stickerId);
		}

		public IReadOnlyList<EarnedSticker> GetStickersFor(string profileId)
		{
			if (string.IsNullOrEmpty(profileId))
				throw new ArgumentException("profileId must not be empty.", nameof(profileId));

			return _save.Schema.Shared.TryGetValue(profileId, out var shared) ? shared.Stickers : EmptyStickers;
		}

		private SharedProfileData GetOrCreateShared(string profileId)
		{
			if (!_save.Schema.Shared.TryGetValue(profileId, out var shared))
			{
				shared = new SharedProfileData();
				_save.Schema.Shared[profileId] = shared;
			}
			return shared;
		}

		private static bool ContainsSticker(List<EarnedSticker> bag, string stickerId)
		{
			for (var i = 0; i < bag.Count; i++)
			{
				if (string.Equals(bag[i].Id, stickerId, StringComparison.Ordinal))
					return true;
			}
			return false;
		}
	}
}
