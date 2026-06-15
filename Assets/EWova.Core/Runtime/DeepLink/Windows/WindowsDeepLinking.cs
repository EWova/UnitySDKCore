// Windows .Net Framework 4.6 以上版本才支援註冊表操作
#if (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN) && !(NET_STANDARD_2_0 || NET_STANDARD_2_1)

using System;
using System.IO;

using UnityEngine;

namespace EWova.DeepLink.Win
{
    public static class WindowsDeepLinking
    {
        public static event Action<string> OnDeepLinkActivated
        {
            add => WindowsDeepLinkingCore.OnDeepLinkActivated += value;
            remove => WindowsDeepLinkingCore.OnDeepLinkActivated -= value;
        }

        public static Func<string> OverrideTargetExecutablePath
        {
            get => WindowsDeepLinkingCore.OverrideTargetExecutablePath;
            set => WindowsDeepLinkingCore.OverrideTargetExecutablePath = value;
        }

        public static Func<string> OverrideWmiQuery
        {
            get => WindowsDeepLinkingCore.OverrideWmiQuery;
            set => WindowsDeepLinkingCore.OverrideWmiQuery = value;
        }

        public static void Initialize(string uriScheme)
        {
            string targetProcessName = Application.productName + ".exe";
            string projectDirectory = Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
            string targetExePath = Path.Combine(projectDirectory, targetProcessName);

            WindowsDeepLinkingCore.Initialize(
                uriScheme,
                Application.productName,
                targetExePath,
                Application.persistentDataPath
            );

            Application.focusChanged += OnApplicationFocus;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AfterSceneLoad()
        {
            if (!WindowsDeepLinkingCore.IsInitialized) return;
            WindowsDeepLinkingCore.ProcessCommandLineArgs();
        }

        public static void ResetState()
        {
            Application.focusChanged -= OnApplicationFocus;
            WindowsDeepLinkingCore.ResetState();
        }

        private static void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) return;
            WindowsDeepLinkingCore.CheckRegistryForDeepLink();
        }
    }
}
#endif