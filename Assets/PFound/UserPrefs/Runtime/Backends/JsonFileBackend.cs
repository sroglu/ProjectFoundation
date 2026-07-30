using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace PFound.UserPrefs
{
    internal sealed class JsonFileBackend : IPrefsBackend
    {
        [Serializable]
        private struct Entry
        {
            public string Key;
            public string TypeName;
            public string Json;
        }

        [Serializable]
        private class FileEnvelope
        {
            public List<Entry> Entries = new List<Entry>();
        }

        private readonly string _filePath;
        private readonly IPrefsLogger _logger;
        private readonly Dictionary<string, Entry> _cache = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private bool _dirty;

        public JsonFileBackend(string filePath, IPrefsLogger logger)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _logger = logger ?? new NullLogger();
        }

        public bool TryGet<T>(string key, out T value)
        {
            if (!_cache.TryGetValue(key, out var entry))
            {
                value = default;
                return false;
            }

            try
            {
                value = JsonUtility.FromJson<Wrapper<T>>(entry.Json).Value;
                return true;
            }
            catch (Exception ex)
            {
                _logger.Warn($"JsonFileBackend.TryGet failed to deserialize key '{key}' as {typeof(T).Name}", ex);
                value = default;
                return false;
            }
        }

        public void Set<T>(string key, T value)
        {
            try
            {
                var json = JsonUtility.ToJson(new Wrapper<T> { Value = value });
                _cache[key] = new Entry
                {
                    Key = key,
                    TypeName = typeof(T).FullName,
                    Json = json,
                };
                _dirty = true;
            }
            catch (Exception ex)
            {
                throw new PrefsBackendException(
                    $"JsonFileBackend.Set failed to serialize key '{key}' of type {typeof(T).FullName}.", ex);
            }
        }

        public void Delete(string key)
        {
            if (_cache.Remove(key))
                _dirty = true;
        }

        public bool HasKey(string key) => _cache.ContainsKey(key);

        public void Load()
        {
            _cache.Clear();
            _dirty = false;

            if (!File.Exists(_filePath))
            {
                _logger.Info($"JsonFileBackend: no file at '{_filePath}', starting empty.");
                return;
            }

            try
            {
                var text = File.ReadAllText(_filePath);
                var envelope = JsonUtility.FromJson<FileEnvelope>(text);
                if (envelope?.Entries == null)
                {
                    _logger.Warn($"JsonFileBackend: file '{_filePath}' parsed to empty envelope.");
                    return;
                }
                foreach (var e in envelope.Entries)
                {
                    if (string.IsNullOrEmpty(e.Key)) continue;
                    _cache[e.Key] = e;
                }
                _logger.Info($"JsonFileBackend: loaded {_cache.Count} entries from '{_filePath}'.");
            }
            catch (Exception ex)
            {
                _logger.Warn($"JsonFileBackend: failed to load '{_filePath}', falling back to defaults.", ex);
                _cache.Clear();
            }
        }

        public void Flush()
        {
            if (!_dirty) return;

            try
            {
                var envelope = new FileEnvelope();
                foreach (var kv in _cache) envelope.Entries.Add(kv.Value);
                var text = JsonUtility.ToJson(envelope, prettyPrint: false);

                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var tmp = _filePath + ".tmp";
                File.WriteAllText(tmp, text);
                if (File.Exists(_filePath)) File.Delete(_filePath);
                File.Move(tmp, _filePath);

                _dirty = false;
                _logger.Info($"JsonFileBackend: wrote {envelope.Entries.Count} entries to '{_filePath}'.");
            }
            catch (Exception ex)
            {
                _logger.Error($"JsonFileBackend: failed to write '{_filePath}'.", ex);
                throw new PrefsBackendException($"JsonFileBackend write failed for '{_filePath}'.", ex);
            }
        }

        public async Task FlushAsync(CancellationToken ct)
        {
            if (!_dirty) return;

            FileEnvelope envelope;
            string text;
            try
            {
                envelope = new FileEnvelope();
                foreach (var kv in _cache) envelope.Entries.Add(kv.Value);
                text = JsonUtility.ToJson(envelope, prettyPrint: false);
                _dirty = false;
            }
            catch (Exception ex)
            {
                throw new PrefsBackendException("JsonFileBackend serialization failed.", ex);
            }

            await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var tmp = _filePath + ".tmp";
                File.WriteAllText(tmp, text);
                if (File.Exists(_filePath)) File.Delete(_filePath);
                File.Move(tmp, _filePath);
            }, ct).ConfigureAwait(false);

            _logger.Info($"JsonFileBackend: async wrote {envelope.Entries.Count} entries to '{_filePath}'.");
        }

        [Serializable]
        private struct Wrapper<T>
        {
            public T Value;
        }
    }
}
