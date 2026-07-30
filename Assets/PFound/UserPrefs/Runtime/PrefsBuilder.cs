using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PFound.UserPrefs
{
    public sealed class PrefsBuilder
    {
        private readonly Dictionary<string, object> _keys = new Dictionary<string, object>(StringComparer.Ordinal);
        private readonly Dictionary<string, PrefStorage> _routes = new Dictionary<string, PrefStorage>(StringComparer.Ordinal);
        private readonly Dictionary<string, Type> _types = new Dictionary<string, Type>(StringComparer.Ordinal);
        private readonly List<MigrationEntry> _migrations = new List<MigrationEntry>();

        private int _schemaVersion = 1;
        private string _jsonFileName;
        private UserPrefsSettings _settings;
        private IPrefsLogger _logger;
        private IPrefsBackend _customPlayerPrefsBackend;
        private IPrefsBackend _customJsonBackend;
        private bool _built;

        public PrefsBuilder Add<T>(PrefKey<T> key)
        {
            if (_keys.ContainsKey(key.Key))
                throw new ArgumentException($"Duplicate PrefKey '{key.Key}'.", nameof(key));

            _keys[key.Key] = key;
            _types[key.Key] = typeof(T);
            _routes[key.Key] = PrefStorageResolver.Resolve(key.Storage, typeof(T));
            return this;
        }

        public PrefsBuilder SchemaVersion(int version)
        {
            if (version < 1) throw new ArgumentException("SchemaVersion must be >= 1.", nameof(version));
            _schemaVersion = version;
            return this;
        }

        public PrefsBuilder MigrationFrom(int version, int to, Action<MigrationContext> migrate)
        {
            if (migrate == null) throw new ArgumentNullException(nameof(migrate));
            if (to != version + 1)
                throw new ArgumentException($"MigrationFrom requires to == version + 1 (got version={version}, to={to}).");
            foreach (var m in _migrations)
                if (m.FromVersion == version && m.ToVersion == to)
                    throw new ArgumentException($"Duplicate migration registered for {version} -> {to}.");
            _migrations.Add(new MigrationEntry(version, to, migrate));
            return this;
        }

        public PrefsBuilder WithJsonStorage(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("JSON file name must be non-empty.", nameof(fileName));
            if (fileName.IndexOfAny(new[] { '/', '\\' }) >= 0)
                throw new ArgumentException("JSON file name must not contain path separators.", nameof(fileName));
            _jsonFileName = fileName;
            return this;
        }

        public PrefsBuilder WithBackend(IPrefsBackend backend, PrefStorage role)
        {
            if (backend == null) throw new ArgumentNullException(nameof(backend));
            if (role == PrefStorage.PlayerPrefs) _customPlayerPrefsBackend = backend;
            else if (role == PrefStorage.JsonFile) _customJsonBackend = backend;
            else throw new ArgumentException("WithBackend requires PrefStorage.PlayerPrefs or PrefStorage.JsonFile.", nameof(role));
            return this;
        }

        public PrefsBuilder WithSettings(UserPrefsSettings settings)
        {
            _settings = settings;
            return this;
        }

        public PrefsBuilder WithLogger(IPrefsLogger logger)
        {
            _logger = logger;
            return this;
        }

        public IPrefsStore Build()
        {
            if (_built) throw new InvalidOperationException("PrefsBuilder.Build() may only be called once.");
            _built = true;

            var settings = _settings; // may be null
            var fileName = _jsonFileName ?? settings?.JsonFileName ?? "userprefs.json";
            var verbose = settings?.VerboseLogging ?? false;
            var logger = _logger ?? new UnityDebugLogger(verbose);

            var problems = new List<string>();

            // Validate keys serialize via resolved backend (basic type check)
            foreach (var kv in _types)
            {
                if (!_routes.TryGetValue(kv.Key, out var storage)) continue;
                if (storage == PrefStorage.PlayerPrefs && !IsPlayerPrefsCompatible(kv.Value))
                    problems.Add($"Key '{kv.Key}' is routed to PlayerPrefs but type '{kv.Value.FullName}' is not supported.");
            }

            // Validate migration chain (if any)
            _migrations.Sort((a, b) => a.FromVersion.CompareTo(b.FromVersion));
            for (int i = 0; i < _migrations.Count; i++)
            {
                if (_migrations[i].ToVersion > _schemaVersion)
                    problems.Add($"Migration {_migrations[i].FromVersion}->{_migrations[i].ToVersion} exceeds current schema version {_schemaVersion}.");
            }

            if (problems.Count > 0)
                throw new PrefsConfigurationException(problems);

            var primitive = _customPlayerPrefsBackend ?? new PlayerPrefsBackend(logger);
            var jsonPath = Path.Combine(Application.persistentDataPath, fileName);
            var json = _customJsonBackend ?? new JsonFileBackend(jsonPath, logger);

            var store = new PrefsStore(_schemaVersion, _keys, _routes, primitive, json, logger, _migrations);
            store.Initialize();

            return store;
        }

        internal IReadOnlyList<MigrationEntry> Migrations => _migrations;
        internal int CurrentSchemaVersion => _schemaVersion;

        private static bool IsPlayerPrefsCompatible(Type t)
        {
            return t == typeof(bool) || t == typeof(int) || t == typeof(long) ||
                   t == typeof(float) || t == typeof(double) || t == typeof(string) ||
                   t.IsEnum;
        }

        internal readonly struct MigrationEntry
        {
            public readonly int FromVersion;
            public readonly int ToVersion;
            public readonly Action<MigrationContext> Migrate;

            public MigrationEntry(int from, int to, Action<MigrationContext> migrate)
            {
                FromVersion = from;
                ToVersion = to;
                Migrate = migrate;
            }
        }
    }
}
