using UnityEngine;

namespace EWova.DeepLink
{
    public class DeepLinkConfig : ScriptableObject
    {
        public const string ResourceName = "DeeplinkConfig";

        public static DeepLinkConfig LoadOrDefault()
        {
            var config = Resources.Load<DeepLinkConfig>(ResourceName);
            return config;
        }

        /// <summary>
        /// 此應用程式的 DeepLink Scheme 
        /// </summary>
        public string MyAppScheme = "example";
    }
}