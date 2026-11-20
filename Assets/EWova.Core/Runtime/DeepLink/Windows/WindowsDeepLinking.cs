using System;
using System.IO;

using UnityEngine;
namespace EWova.DeepLink.Win
{
    public static class WindowsDeepLinking
    {
        // EWova.DeepLink.Win.Core 為 netstandard2.0，不論 Api Compatibility Level 為 .NET Standard 或 .NET Framework 都可使用
#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
#if UNITY_EDITOR
        /// <summary>
        /// 1.10.1 用來依 Editor Assemblies Compatibility Level 開關編輯器支援；1.10.2 起 Windows 編輯器一律支援，固定為 true
        /// </summary>
        [Obsolete("Windows 編輯器一律支援 DeepLink，不再需要設定")]
        public static bool IsEditorSupported { get => true; set { } }
#endif

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

            // VBS 只在編輯器使用（Unity.exe 旁不會有啟動器）；正式 build 寫出 VBS 並註冊容易被防毒誤判
            WindowsDeepLinkingCore.Initialize(
                uriScheme,
                Application.productName,
                targetExePath,
                Application.persistentDataPath,
                allowAgentFallback: Application.isEditor
            );

            if (WindowsDeepLinkingCore.Registration == WinDeepLinkRegistration.None)
            {
                Debug.LogError($"[EWova]DeepLink 找不到 {WindowsDeepLinkingCore.LauncherFileName}（應在 {targetProcessName} 旁），{uriScheme}:// 將無法開啟此程式。請發布完整的 build 資料夾。");
            }

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
#if UNITY_EDITOR
        /// <summary>
        /// 非 Windows 編輯器（例如 Mac 編輯器的 Build Target 為 Windows）不支援，固定為 false
        /// </summary>
        [Obsolete("Windows 編輯器一律支援 DeepLink，不再需要設定")]
        public static bool IsEditorSupported { get => false; set { } }
#endif

#pragma warning disable CS0067
        public static event Action<string> OnDeepLinkActivated;
        public static Func<string> OverrideTargetExecutablePath;
        public static Func<string> OverrideWmiQuery;
        public static void ResetState() { }
#pragma warning restore CS0067

#endif
    }
}
