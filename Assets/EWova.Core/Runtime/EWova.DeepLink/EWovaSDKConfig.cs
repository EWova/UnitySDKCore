using UnityEngine;

namespace EWova.DeepLink
{
    public class EWovaSDKConfig : ScriptableObject
    {
        public const string ResourceName = "EWova SDK Config";
        public const string OldResourceName = "DeeplinkConfig";

        public static EWovaSDKConfig LoadOrDefault()
        {
            var config = Resources.Load<EWovaSDKConfig>(ResourceName);

            if (config == null)
            {
                // 嘗試載入舊資源名稱的 Config
                config = Resources.Load<EWovaSDKConfig>(OldResourceName);
#if UNITY_EDITOR
                // 如果找到舊資源，將其重新命名為新的資源名稱
                config.name = ResourceName;
                UnityEditor.AssetDatabase.SaveAssets();
                UnityEditor.AssetDatabase.Refresh();
#endif
            }

            return config;
        }

        /// <summary>
        /// 此應用程式的 DeepLink Scheme 
        /// </summary>
        public string MyAppScheme = "example";
    }
}