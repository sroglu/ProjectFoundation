using System.Collections.Generic;

namespace PFound.UserPrefs
{
    public sealed class MigrationContext
    {
        public IPrefsBackend PrimitiveBackend { get; }
        public IDictionary<string, object> JsonData { get; }

        internal MigrationContext(IPrefsBackend primitiveBackend, IDictionary<string, object> jsonData)
        {
            PrimitiveBackend = primitiveBackend;
            JsonData = jsonData;
        }

        public bool TryGetJsonKey<T>(string key, out T value)
        {
            if (JsonData != null && JsonData.TryGetValue(key, out var raw) && raw is T typed)
            {
                value = typed;
                return true;
            }
            value = default;
            return false;
        }

        public void SetJsonKey(string key, object value)
        {
            if (JsonData == null) return;
            JsonData[key] = value;
        }

        public void RemoveJsonKey(string key) => JsonData?.Remove(key);
    }
}
