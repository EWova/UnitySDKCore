using System;
using System.IO;

using UnityEngine;
namespace EWova.DeepLink.Win
{
    public static class WindowsDeepLinking
    {
        // Windows .Net Framework 4.6 以上版本才支援註冊表操作
#if (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN) && !(NET_STANDARD_2_0 || NET_STANDARD_2_1)
        public static event Action<string, DeepLinkInvocationType> OnDeepLinkActivated;

        private static void OnDeepLinkActivatedInternal(WinActivatedDeepLink winActivated, DeepLinkInvocationType pendingType)
        {
            OnDeepLinkActivated?.Invoke(winActivated.Uri, pendingType);
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

            if (Application.isEditor)
            {
                // 在編輯器模式下，檢查註冊表以處理深層連結
                WindowsDeepLinkingCore.CheckRegistryForDeepLink();
            }
            else
            {
                // 在非編輯器模式下，處理命令列參數以處理深層連結。並清除註冊表中的深層連結值，以避免重複處理
                WindowsDeepLinkingCore.ProcessCommandLineArgs();
                WindowsDeepLinkingCore.ClearRegistryDeepLinkValue();
            }

            var pendingWinActivatedDeepLink = WindowsDeepLinkingCore.CurrentWinActivatedDeepLink;
            if (pendingWinActivatedDeepLink.HasValue)
            {
                OnDeepLinkActivatedInternal(
                    pendingWinActivatedDeepLink.Value
                    , DeepLinkInvocationType.Launch);
            }

            WindowsDeepLinkingCore.OnDeepLinkActivated += WindowsDeepLinkingCore_OnDeepLinkActivated;
            Application.focusChanged += OnApplicationFocus;
        }

        private static void WindowsDeepLinkingCore_OnDeepLinkActivated(WinActivatedDeepLink obj)
        {
            OnDeepLinkActivatedInternal(obj, DeepLinkInvocationType.Runtime);
        }

        public static void ResetState()
        {
            Application.focusChanged -= OnApplicationFocus;
            WindowsDeepLinkingCore.OnDeepLinkActivated -= WindowsDeepLinkingCore_OnDeepLinkActivated;
            WindowsDeepLinkingCore.ResetState();
        }

        private static void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                return;

            WindowsDeepLinkingCore.CheckRegistryForDeepLink();
        }
#else

#pragma warning disable CS0067
        public static event Action<string> OnDeepLinkActivated;
        public static Func<string> OverrideTargetExecutablePath;
        public static Func<string> OverrideWmiQuery;
        public static void ResetState() { }
#pragma warning restore CS0067

#endif
    }
}
