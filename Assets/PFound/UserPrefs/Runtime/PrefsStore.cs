using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PFound.UserPrefs
{
    internal sealed class PrefsStore : IPrefsStore
    {
        private readonly Dictionary<string, object> _keys; // PrefKey<T> boxed for type-erased lookup
        private readonly Dictionary<string, PrefStorage> _routes;
        private readonly IPrefsBackend _primitiveBackend;
        private readonly IPrefsBackend _jsonBackend;
        private readonly IPrefsLogger _logger;
        private readonly IReadOnlyList<PrefsBuilder.MigrationEntry> _migrations;
        private readonly Dictionary<string, Delegate> _subscribers = new Dictionary<string, Delegate>(StringComparer.Ordinal);

        private PrefsLifecycleHook _lifecycle;
        private bool _disposed;

        public int SchemaVersion { get; }
        public bool IsLoaded { get; private set; }

        internal PrefsStore(
            int schemaVersion,
            Dictionary<string, object> keys,
            Dictionary<string, PrefStorage> routes,
            IPrefsBackend primitiveBackend,
            IPrefsBackend jsonBackend,
            IPrefsLogger logger,
            IReadOnlyList<PrefsBuilder.MigrationEntry> migrations)
        {
            SchemaVersion = schemaVersion;
            _keys = keys;
            _routes = routes;
            _primitiveBackend = primitiveBackend;
            _jsonBackend = jsonBackend;
            _logger = logger ?? new NullLogger();
            _migrations = migrations ?? Array.Empty<PrefsBuilder.MigrationEntry>();
        }

        internal void Initialize()
        {
            _logger.Info($"PrefsStore initializing (schemaVersion={SchemaVersion}, keys={_keys.Count}).");
            _primitiveBackend.Load();
            _jsonBackend.Load();
            PrefsMigrationRunner.Run(_primitiveBackend, SchemaVersion, _migrations, _logger);
            _lifecycle = PrefsLifecycleHook.Spawn(this, _logger);
            IsLoaded = true;
            _logger.Info("PrefsStore loaded.");
        }

        public T Get<T>(PrefKey<T> key)
        {
            EnsureLoaded();
            var backend = ResolveBackend(key.Key);
            if (backend.TryGet(key.Key, out T value))
                return value;
            return key.DefaultValue;
        }

        public void Set<T>(PrefKey<T> key, T value)
        {
            EnsureLoaded();
            var backend = ResolveBackend(key.Key);

            T current = backend.TryGet(key.Key, out T existing) ? existing : key.DefaultValue;
            if (EqualityComparer<T>.Default.Equals(current, value))
                return; // no-op write, no event

            backend.Set(key.Key, value);
            Dispatch(new PrefChange<T>(key.Key, current, value, PrefChangeKind.Set));
        }

        public void Clear<T>(PrefKey<T> key)
        {
            EnsureLoaded();
            var backend = ResolveBackend(key.Key);

            T current = backend.TryGet(key.Key, out T existing) ? existing : key.DefaultValue;
            backend.Delete(key.Key);
            Dispatch(new PrefChange<T>(key.Key, current, key.DefaultValue, PrefChangeKind.Cleared));
        }

        public void SetDeferred<T>(PrefKey<T> key, T value)
        {
            // v1: same as Set, since both backends already cache and require Flush() for durability.
            // SetDeferred exists for forward compatibility with batched-write backends.
            Set(key, value);
        }

        public void Flush()
        {
            if (_disposed) return;
            _primitiveBackend.Flush();
            _jsonBackend.Flush();
        }

        public async Task FlushAsync(CancellationToken ct = default)
        {
            if (_disposed) return;
            await _primitiveBackend.FlushAsync(ct).ConfigureAwait(false);
            await _jsonBackend.FlushAsync(ct).ConfigureAwait(false);
        }

        public IDisposable Subscribe<T>(PrefKey<T> key, Action<PrefChange<T>> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            EnsureLoaded();

            if (_subscribers.TryGetValue(key.Key, out var existing))
            {
                _subscribers[key.Key] = Delegate.Combine(existing, handler);
            }
            else
            {
                _subscribers[key.Key] = handler;
            }
            return new SubscriptionToken(this, key.Key, handler);
        }

        internal void Unsubscribe(string key, Delegate handler)
        {
            if (_subscribers.TryGetValue(key, out var existing))
            {
                var remaining = Delegate.Remove(existing, handler);
                if (remaining == null)
                    _subscribers.Remove(key);
                else
                    _subscribers[key] = remaining;
            }
        }

        private void Dispatch<T>(PrefChange<T> change)
        {
            if (!_subscribers.TryGetValue(change.Key, out var d)) return;
            // Snapshot to allow handlers to subscribe/unsubscribe during dispatch
            // without affecting in-flight delivery.
            var typed = (Action<PrefChange<T>>)d;
            typed.Invoke(change);
        }

        private IPrefsBackend ResolveBackend(string key)
        {
            return _routes.TryGetValue(key, out var s) && s == PrefStorage.JsonFile
                ? _jsonBackend
                : _primitiveBackend;
        }

        private void EnsureLoaded()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PrefsStore));
            if (!IsLoaded) throw new PrefsNotLoadedException();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { Flush(); } catch (Exception ex) { _logger.Warn("Flush during Dispose failed.", ex); }
            _lifecycle?.Detach();
            _lifecycle = null;
            _subscribers.Clear();
            IsLoaded = false;
        }

        private sealed class SubscriptionToken : IDisposable
        {
            private PrefsStore _store;
            private readonly string _key;
            private readonly Delegate _handler;

            public SubscriptionToken(PrefsStore store, string key, Delegate handler)
            {
                _store = store;
                _key = key;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_store == null) return;
                _store.Unsubscribe(_key, _handler);
                _store = null;
            }
        }
    }
}
