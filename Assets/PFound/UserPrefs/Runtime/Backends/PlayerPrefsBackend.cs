using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace PFound.UserPrefs
{
    internal sealed class PlayerPrefsBackend : IPrefsBackend
    {
        private readonly IPrefsLogger _logger;
        private bool _dirty;

        public PlayerPrefsBackend(IPrefsLogger logger)
        {
            _logger = logger ?? new NullLogger();
        }

        public bool TryGet<T>(string key, out T value)
        {
            if (!PlayerPrefs.HasKey(key))
            {
                value = default;
                return false;
            }

            try
            {
                var t = typeof(T);
                if (t == typeof(bool))
                {
                    value = (T)(object)(PlayerPrefs.GetInt(key) != 0);
                    return true;
                }
                if (t == typeof(int))
                {
                    value = (T)(object)PlayerPrefs.GetInt(key);
                    return true;
                }
                if (t == typeof(long))
                {
                    var s = PlayerPrefs.GetString(key);
                    if (long.TryParse(s, out var l))
                    {
                        value = (T)(object)l;
                        return true;
                    }
                    value = default;
                    return false;
                }
                if (t == typeof(float))
                {
                    value = (T)(object)PlayerPrefs.GetFloat(key);
                    return true;
                }
                if (t == typeof(double))
                {
                    var s = PlayerPrefs.GetString(key);
                    if (double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d))
                    {
                        value = (T)(object)d;
                        return true;
                    }
                    value = default;
                    return false;
                }
                if (t == typeof(string))
                {
                    value = (T)(object)PlayerPrefs.GetString(key);
                    return true;
                }
                if (t.IsEnum)
                {
                    var i = PlayerPrefs.GetInt(key);
                    value = (T)Enum.ToObject(t, i);
                    return true;
                }

                value = default;
                return false;
            }
            catch (Exception ex)
            {
                _logger.Warn($"PlayerPrefsBackend.TryGet failed for key '{key}'", ex);
                value = default;
                return false;
            }
        }

        public void Set<T>(string key, T value)
        {
            var t = typeof(T);
            if (t == typeof(bool))
                PlayerPrefs.SetInt(key, ((bool)(object)value) ? 1 : 0);
            else if (t == typeof(int))
                PlayerPrefs.SetInt(key, (int)(object)value);
            else if (t == typeof(long))
                PlayerPrefs.SetString(key, ((long)(object)value).ToString(System.Globalization.CultureInfo.InvariantCulture));
            else if (t == typeof(float))
                PlayerPrefs.SetFloat(key, (float)(object)value);
            else if (t == typeof(double))
                PlayerPrefs.SetString(key, ((double)(object)value).ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            else if (t == typeof(string))
                PlayerPrefs.SetString(key, (string)(object)value ?? string.Empty);
            else if (t.IsEnum)
                PlayerPrefs.SetInt(key, Convert.ToInt32(value));
            else
                throw new PrefsBackendException(
                    $"PlayerPrefsBackend does not support type '{t.FullName}'. Use JsonFile storage.");

            _dirty = true;
        }

        public void Delete(string key)
        {
            if (PlayerPrefs.HasKey(key))
            {
                PlayerPrefs.DeleteKey(key);
                _dirty = true;
            }
        }

        public bool HasKey(string key) => PlayerPrefs.HasKey(key);

        public void Load() { }

        public void Flush()
        {
            if (!_dirty) return;
            PlayerPrefs.Save();
            _dirty = false;
        }

        public Task FlushAsync(CancellationToken ct)
        {
            Flush();
            return Task.CompletedTask;
        }
    }
}
