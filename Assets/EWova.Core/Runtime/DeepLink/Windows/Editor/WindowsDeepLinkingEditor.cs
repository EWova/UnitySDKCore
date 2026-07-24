#if UNITY_EDITOR_WIN && !(NET_STANDARD_2_0 || NET_STANDARD_2_1)
using UnityEditor;

using UnityEngine;

namespace EWova.DeepLink.Win.Editor
{
    public static class WindowsDeepLinkingEditor
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void InjectEditorOverrides()
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

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        }
        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode)
                return;

            WindowsDeepLinking.ResetState();
        }
    }
}
#endif