using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace PFound.HubApp.Save
{
	/// <summary>
	/// Migrates a save's raw JSON tree (JObject) from one schema version to the next.
	/// Implementations operate on JObject — NOT on POCO types — so older migrations
	/// remain valid even after SaveSchema's C# fields rename/remove later.
	/// </summary>
	public interface IMigration
	{
		int FromVersion { get; }
		int ToVersion { get; }
		void Apply(JObject root);
	}

	/// <summary>
	/// Sequential schema upgrader. Registers a set of <see cref="IMigration"/> steps,
	/// then upgrades a save tree from <c>currentVersion</c> to <c>targetVersion</c> by
	/// applying steps in order (vN → vN+1 → … → vTarget).
	/// </summary>
	/// <remarks>
	/// Fail-fast: throws on any structural inconsistency (gap in chain, downgrade attempt,
	/// duplicate registration). Never silently skips a step or falls through.
	/// </remarks>
	public class MigrationPipeline
	{
		// Key: FromVersion. Value: the migration that takes you to FromVersion+1.
		private readonly Dictionary<int, IMigration> _byFrom = new Dictionary<int, IMigration>();

		public void Register(IMigration migration)
		{
			if (migration == null)
				throw new ArgumentNullException(nameof(migration));
			if (migration.ToVersion != migration.FromVersion + 1)
				throw new ArgumentException(
					$"Migration must step exactly +1 version. Got {migration.FromVersion}→{migration.ToVersion}.",
					nameof(migration));
			if (_byFrom.ContainsKey(migration.FromVersion))
				throw new InvalidOperationException(
					$"A migration from version {migration.FromVersion} is already registered.");

			_byFrom.Add(migration.FromVersion, migration);
		}

		/// <summary>
		/// Upgrades <paramref name="root"/> in-place from <paramref name="currentVersion"/>
		/// to <paramref name="targetVersion"/>. Throws if any step in the chain is missing
		/// or if a downgrade is requested.
		/// </summary>
		public void Apply(JObject root, int currentVersion, int targetVersion)
		{
			if (root == null)
				throw new ArgumentNullException(nameof(root));
			if (currentVersion > targetVersion)
				throw new InvalidOperationException(
					$"Schema downgrade not supported (save is v{currentVersion}, code is v{targetVersion}). Aborting load to avoid data loss.");

			var v = currentVersion;
			while (v < targetVersion)
			{
				if (!_byFrom.TryGetValue(v, out var step))
					throw new InvalidOperationException(
						$"No migration registered from v{v} → v{v + 1}. Save cannot be brought up to v{targetVersion}.");

				step.Apply(root);
				root["SchemaVersion"] = step.ToVersion;
				v = step.ToVersion;
			}
		}
	}
}
