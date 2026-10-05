using System.IO;

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

using UnityEngine;

namespace EWova.DeepLink.Win.Editor
{
    /// <summary>
    /// 把 DeepLinkLauncher.exe 複製到 Windows 輸出的 exe 旁邊。WindowsDeepLinkingCore 啟動時看到它，就改用它處理 Scheme，
    /// 不再產生 VBS（容易被防毒誤判）。正式 build 找不到它時 DeepLink 不會註冊（VBS 只在編輯器使用）。
    /// </summary>
    internal sealed class DeepLinkLauncherPostprocessBuild : IPostprocessBuildWithReport
    {
        // DeepLinkLauncher.exe.meta 的 guid；檔名需與 WindowsDeepLinkingCore.LauncherFileName 一致
        private const string LauncherGuid = "f894d9ff78df493c9371c9b0d63b891c";
        private const string LauncherFileName = "DeepLinkLauncher.exe";

        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            BuildTarget target = report.summary.platform;
            if (target != BuildTarget.StandaloneWindows && target != BuildTarget.StandaloneWindows64)
                return;

            string sourcePath = AssetDatabase.GUIDToAssetPath(LauncherGuid);
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            {
                Debug.LogError($"[EWova]DeepLink 找不到 {LauncherFileName}，此 build 的 Windows DeepLink 將無法使用");
                return;
            }

            string outputDirectory = Path.GetDirectoryName(report.summary.outputPath);
            File.Copy(Path.GetFullPath(sourcePath), Path.Combine(outputDirectory, LauncherFileName), overwrite: true);
        }
    }
}
