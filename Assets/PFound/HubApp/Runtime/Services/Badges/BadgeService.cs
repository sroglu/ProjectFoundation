using System;
using System.Collections.Generic;
using PFound.HubApp.MiniGame.Signals;
using PFound.HubApp.Save;
using PFound.Signaling;

namespace PFound.HubApp.Services.Badges
{
	/// <summary>
	/// Concrete <see cref="IBadgeService"/>. The shared-section subtree
	/// (<c>Schema.Shared[profileId]</c>) is auto-created on first award so callers don't have to
	/// pre-seed it; everything else is a fail-fast lookup.
	/// </summary>
	public class BadgeService : IBadgeService
	{
		// An empty-list singleton is returned for "profile has no shared subtree yet" so
		// GetBadgesFor stays allocation-free in the common pre-earn read case (Home screen
		// renders the gallery on every paint).
		private static readonly IReadOnlyList<EarnedBadge> EmptyBadges = Array.Empty<EarnedBadge>();

		private readonly ISaveService _save;
		private readonly SignalTracker _signals;

		public BadgeService(ISaveService save, SignalTracker signals)
		{
			if (save == null) throw new ArgumentNullException(nameof(save));
			if (signals == null) throw new ArgumentNullException(nameof(signals));

			_save = save;
			_signals = signals;
		}

		public bool Award(string profileId, string badgeId, string sourceGame)
		{
			if (string.IsNullOrEmpty(profileId))
				throw new ArgumentException("profileId must not be empty.", nameof(profileId));
			if (string.IsNullOrEmpty(badgeId))
				throw new ArgumentException("badgeId must not be empty.", nameof(badgeId));

			var bag = GetOrCreateShared(profileId).Badges;
			if (ContainsBadge(bag, badgeId))
				return false;

			bag.Add(new EarnedBadge
			{
				Id = badgeId,
				EarnedAt = DateTime.UtcNow,
				SourceGame = sourceGame
			});

			_signals.Queue<BadgePersistedSignal>(causer: null);
			return true;
		}

		public bool HasBadge(string profileId, string badgeId)
		{
			if (string.IsNullOrEmpty(profileId))
				throw new ArgumentException("profileId must not be empty.", nameof(profileId));
			if (string.IsNullOrEmpty(badgeId))
				throw new ArgumentException("badgeId must not be empty.", nameof(badgeId));

			return _save.Schema.Shared.TryGetValue(profileId, out var shared)
			    && ContainsBadge(shared.Badges, badgeId);
		}

		public IReadOnlyList<EarnedBadge> GetBadgesFor(string profileId)
		{
			if (string.IsNullOrEmpty(profileId))
				throw new ArgumentException("profileId must not be empty.", nameof(profileId));

			return _save.Schema.Shared.TryGetValue(profileId, out var shared) ? shared.Badges : EmptyBadges;
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

		private static bool ContainsBadge(List<EarnedBadge> bag, string badgeId)
		{
			for (var i = 0; i < bag.Count; i++)
			{
				if (string.Equals(bag[i].Id, badgeId, StringComparison.Ordinal))
					return true;
			}
			return false;
		}
	}
}
