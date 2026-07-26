using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

namespace EWova.DeepLink.Editor
{
    public class CreateConfigEditor
    {
        internal const string ResourceFolderPath = "Assets/Resources";
        internal const string AssetPath = "Assets/Resources/" + DeepLinkConfig.ResourceName + ".asset";
        internal const string MenuPath = "EWova/DeepLink/Create Config";

        [MenuItem(MenuPath, false, 1)]
        public static void CreateConfig()
        {
            // Ensure Resources folder exists
            if (!Directory.Exists(ResourceFolderPath))
            {
                Directory.CreateDirectory(ResourceFolderPath);
                AssetDatabase.Refresh();
            }

            // Create asset
            DeepLinkConfig config = DeepLinkConfig.EditorLoadOrCreateResource(out bool isNewlyCreated);

            if (isNewlyCreated)
            {
                ConfigUtility.EnsurePreloaded(config);

                EditorUtility.FocusProjectWindow();
                Selection.activeObject = config;
            }
        }

        // 控制 Menu 是否可點擊
        [MenuItem(MenuPath, true)]
        public static bool ValidateCreateConfig()
        {
            return DeepLinkConfig.LoadOrDefault() == null;
        }
    }

    [InitializeOnLoad]
    public static class ConfigPreloadValidator
    {
        static ConfigPreloadValidator()
        {
            EditorApplication.delayCall += Check;
        }

        private static void Check()
        {
            if (BuildPipeline.isBuildingPlayer)
                return;

            if (!DeepLinkConfig.IsResourceExist(out var asset))
                return;

            ConfigUtility.EnsurePreloaded(asset);
        }
    }

    internal static class ConfigUtility
    {
        public static void EnsurePreloaded(DeepLinkConfig config)
        {
            var preloadedAssets = PlayerSettings.GetPreloadedAssets();
            var list = preloadedAssets.ToList();

            bool exists = list.Any(a => a == config);
            if (exists)
                return;

            // 移除舊的 Config
            list.RemoveAll(a => a is DeepLinkConfig);

            list.Add(config);

            PlayerSettings.SetPreloadedAssets(list.ToArray());

            UnityEngine.Debug.Log("EWovaSDKConfig 已加入 Preloaded Assets");
        }
    }
}