using UnityEngine;

namespace EWova
{
    //PlayerPrefs
    public interface IPrefsProvider

    {
        void SetInt(string key, int value);
        int GetInt(string key, int defaultValue);
        int GetInt(string key);
        void SetFloat(string key, float value);
        float GetFloat(string key, float defaultValue);
        float GetFloat(string key);
        void SetString(string key, string value);
        string GetString(string key, string defaultValue);
        string GetString(string key);
        bool HasKey(string key);
        void DeleteKey(string key);
        void DeleteAll();
        void Save();
    }
}
