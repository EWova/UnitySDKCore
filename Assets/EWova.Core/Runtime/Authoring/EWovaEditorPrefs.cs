#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;

using UnityEditor;

using UnityEngine;

namespace EWova.Authoring
{
    [FilePath("UserSettings/EWovaEditorPrefs.yaml", FilePathAttribute.Location.ProjectFolder)]
    public class EWovaEditorPrefs : ScriptableSingleton<EWovaEditorPrefs>, ISerializationCallbackReceiver
    {
        [Serializable]
        public struct Entry
        {
            public string key;
            public string value;

            public Entry(string key, string value)
            {
                this.key = key;
                this.value = value;
            }
        }

        [SerializeField]
        private List<Entry> serializedEntries = new List<Entry>();

        private Dictionary<string, string> _prefsDictionary = new Dictionary<string, string>();

        public void OnBeforeSerialize()
        {
            serializedEntries.Clear();
            foreach (var kvp in _prefsDictionary)
            {
                serializedEntries.Add(new Entry(kvp.Key, kvp.Value));
            }
        }

        public void OnAfterDeserialize()
        {
            _prefsDictionary.Clear();
            foreach (var entry in serializedEntries)
            {
                if (!string.IsNullOrEmpty(entry.key))
                {
                    _prefsDictionary[entry.key] = entry.value;
                }
            }
        }

        private static string TryGetValue(string key, object defaultValue = null)
        {
            if (instance._prefsDictionary.TryGetValue(key, out var value))
            {
                return value;
            }
            return defaultValue?.ToString();
        }

        private static void SetValue(string key, string stringValue)
        {
            instance._prefsDictionary[key] = stringValue;
            instance.Save(true);
        }

        public static string GetString(string key, string defaultValue = null) => TryGetValue(key, defaultValue);
        public static int GetInt(string key, int defaultValue = 0) => int.TryParse(TryGetValue(key, defaultValue), out var v) ? v : defaultValue;
        public static float GetFloat(string key, float defaultValue = 0f) => float.TryParse(TryGetValue(key, defaultValue), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : defaultValue;
        public static bool GetBool(string key, bool defaultValue = false) => bool.TryParse(TryGetValue(key, defaultValue), out var v) ? v : defaultValue;

        public static void SetString(string key, string value) => SetValue(key, value);
        public static void SetInt(string key, int value) => SetValue(key, value.ToString());
        public static void SetFloat(string key, float value) => SetValue(key, value.ToString(CultureInfo.InvariantCulture));
        public static void SetBool(string key, bool value) => SetValue(key, value.ToString());
    }
}
#endif