using System;
using System.Collections.Generic;

namespace PFound.UserPrefs
{
    public abstract class PrefsException : Exception
    {
        protected PrefsException(string message) : base(message) { }
        protected PrefsException(string message, Exception inner) : base(message, inner) { }
    }

    public sealed class PrefsNotLoadedException : PrefsException
    {
        public PrefsNotLoadedException()
            : base("PrefsStore has not finished loading. Resolve IPrefsStore from DI after registration completes.") { }
    }

    public sealed class PrefsConfigurationException : PrefsException
    {
        public IReadOnlyList<string> Problems { get; }

        public PrefsConfigurationException(IReadOnlyList<string> problems)
            : base(BuildMessage(problems))
        {
            Problems = problems;
        }

        public PrefsConfigurationException(string singleProblem)
            : this(new[] { singleProblem }) { }

        private static string BuildMessage(IReadOnlyList<string> problems)
        {
            if (problems == null || problems.Count == 0)
                return "PrefsBuilder configuration failed.";
            return "PrefsBuilder configuration failed: " + string.Join("; ", problems);
        }
    }

    public sealed class PrefsMigrationException : PrefsException
    {
        public int FromVersion { get; }
        public int ToVersion { get; }

        public PrefsMigrationException(int fromVersion, int toVersion, Exception inner)
            : base($"Migration from version {fromVersion} to {toVersion} failed: {inner.Message}", inner)
        {
            FromVersion = fromVersion;
            ToVersion = toVersion;
        }

        public PrefsMigrationException(string message)
            : base(message) { }
    }

    public sealed class PrefsSchemaDowngradeException : PrefsException
    {
        public int StoredVersion { get; }
        public int CurrentVersion { get; }

        public PrefsSchemaDowngradeException(int storedVersion, int currentVersion)
            : base($"Stored UserPrefs schema version ({storedVersion}) is newer than current ({currentVersion}). " +
                   "Downgrade is not supported. Clear stored data or run a newer build.")
        {
            StoredVersion = storedVersion;
            CurrentVersion = currentVersion;
        }
    }

    public sealed class PrefsBackendException : PrefsException
    {
        public PrefsBackendException(string message) : base(message) { }
        public PrefsBackendException(string message, Exception inner) : base(message, inner) { }
    }
}
