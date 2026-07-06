using System;
using System.Collections.Generic;

namespace PFound.UserPrefs
{
    internal static class PrefsMigrationRunner
    {
        internal const string VersionKey = "__userprefs.version";

        public static int ReadStoredVersion(IPrefsBackend primitive)
        {
            return primitive.TryGet<int>(VersionKey, out var v) ? v : 1;
        }

        public static void Run(
            IPrefsBackend primitive,
            int currentVersion,
            IReadOnlyList<PrefsBuilder.MigrationEntry> migrations,
            IPrefsLogger logger)
        {
            var stored = ReadStoredVersion(primitive);

            if (stored == currentVersion)
            {
                logger?.Info($"PrefsMigrationRunner: stored version matches current ({stored}), no migration.");
                EnsureVersionRecorded(primitive, currentVersion);
                return;
            }

            if (stored > currentVersion)
                throw new PrefsSchemaDowngradeException(stored, currentVersion);

            logger?.Info($"PrefsMigrationRunner: migrating {stored} -> {currentVersion} ({migrations.Count} step(s) registered).");

            // Sort already done in builder; defensive sort here is harmless but avoid mutating caller list.
            var ordered = new List<PrefsBuilder.MigrationEntry>(migrations);
            ordered.Sort((a, b) => a.FromVersion.CompareTo(b.FromVersion));

            foreach (var step in ordered)
            {
                if (step.FromVersion < stored) continue;
                if (step.FromVersion >= currentVersion) break;

                var ctx = new MigrationContext(primitive, new Dictionary<string, object>(StringComparer.Ordinal));
                try
                {
                    logger?.Info($"PrefsMigrationRunner: running {step.FromVersion} -> {step.ToVersion}.");
                    step.Migrate(ctx);
                }
                catch (Exception ex)
                {
                    logger?.Error($"PrefsMigrationRunner: migration {step.FromVersion} -> {step.ToVersion} threw.", ex);
                    throw new PrefsMigrationException(step.FromVersion, step.ToVersion, ex);
                }
                stored = step.ToVersion;
            }

            if (stored != currentVersion)
                throw new PrefsMigrationException(
                    $"Migration chain incomplete: ended at version {stored}, expected {currentVersion}.");

            primitive.Set(VersionKey, currentVersion);
            primitive.Flush();
            logger?.Info($"PrefsMigrationRunner: migrated to {currentVersion}, version key written.");
        }

        private static void EnsureVersionRecorded(IPrefsBackend primitive, int currentVersion)
        {
            if (!primitive.HasKey(VersionKey))
            {
                primitive.Set(VersionKey, currentVersion);
                primitive.Flush();
            }
        }
    }
}
