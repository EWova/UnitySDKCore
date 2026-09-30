using UnityEngine;

namespace EWova
{
    public static class PrefsProvider
    {
        private static IPrefsProvider _provider = null;
        /// <summary>
        /// 自定義PrefsProvider，若 null 則使用 UnityEngine.PlayerPrefs
        /// </summary>
        /// <param name="provider"></param>
        public static void SetProvider(IPrefsProvider provider)
        {
            _provider = provider;
        }
        public static void SetInt(string key, int value)
        { if (_provider == null) PlayerPrefs.SetInt(key, value); else _provider.SetInt(key, value); }
        public static int GetInt(string key, int defaultValue)
            => _provider == null ? PlayerPrefs.GetInt(key, defaultValue) : _provider.GetInt(key, defaultValue);
        public static int GetInt(string key)
            => _provider == null ? PlayerPrefs.GetInt(key) : _provider.GetInt(key);

        public static void SetFloat(string key, float value)
        { if (_provider == null) PlayerPrefs.SetFloat(key, value); else _provider.SetFloat(key, value); }
        public static float GetFloat(string key, float defaultValue)
            => _provider == null ? PlayerPrefs.GetFloat(key, defaultValue) : _provider.GetFloat(key, defaultValue);
        public static float GetFloat(string key)
            => _provider == null ? PlayerPrefs.GetFloat(key) : _provider.GetFloat(key);

        public static void SetString(string key, string value)
        { if (_provider == null) PlayerPrefs.SetString(key, value); else _provider.SetString(key, value); }
        public static string GetString(string key, string defaultValue)
            => _provider == null ? PlayerPrefs.GetString(key, defaultValue) : _provider.GetString(key, defaultValue);
        public static string GetString(string key)
            => _provider == null ? PlayerPrefs.GetString(key) : _provider.GetString(key);

        public static bool HasKey(string key)
            => _provider == null ? PlayerPrefs.HasKey(key) : _provider.HasKey(key);
        public static void DeleteKey(string key)
        { if (_provider == null) PlayerPrefs.DeleteKey(key); else _provider.DeleteKey(key); }
        public static void DeleteAll()
        { if (_provider == null) PlayerPrefs.DeleteAll(); else _provider.DeleteAll(); }

        public static void Save()
        { if (_provider == null) PlayerPrefs.Save(); else _provider.Save(); }
    }
}
