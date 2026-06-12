#if (UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN) && !(NET_STANDARD_2_0 || NET_STANDARD_2_1)
using UnityEditor;

namespace EWova.DeepLink.Win.Editor
{
    [InitializeOnLoad]
    public static class WindowsDeepLinkingEditor
    {
        static WindowsDeepLinkingEditor()
        {
            WindowsDeepLinking.OverrideTargetExecutablePath = () =>
            {
                return EditorApplication.applicationPath;
            };

            WindowsDeepLinking.OverrideWmiQuery = () =>
            {
                int currentPid = System.Diagnostics.Process.GetCurrentProcess().Id;
                return $"Select * from Win32_Process Where ProcessId = {currentPid}";
            };

            EditorApplication.playModeStateChanged += PlayModeStateChanged;
        }

        private static void PlayModeStateChanged(PlayModeStateChange mode)
        {
            if (mode == PlayModeStateChange.EnteredEditMode)
            {
                // 呼叫 Runtime 開放的 API 來清理狀態
                WindowsDeepLinking.ResetState();
            }
        }
    }
}
#endif