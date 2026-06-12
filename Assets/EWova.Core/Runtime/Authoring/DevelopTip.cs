#if UNITY_EDITOR
using UnityEditor;

namespace EWova.Authoring
{
    public static class DevelopTip
    {
        public static bool IsEnabled => !EWovaEditorPrefs.instance.GetBool(PrefKey, false);

        private const string MenuPath = "EWova/Editor/Develop Tip";
        private const string PrefKey = "Env_EditorDisableDevelopTip"; // 使用反向的 key，讓預設值為 false 時功能是關閉的

        [MenuItem(MenuPath, false, 1)]
        private static void Switch()
        {
            var disable = EWovaEditorPrefs.instance.GetBool(PrefKey, false);
            var setTo = !disable;
            EWovaEditorPrefs.instance.SetBool(PrefKey, setTo);

            if (setTo)
            {
                EditorLogger.Info("關閉編輯器開發提示");
            }
            else
            {
                EditorLogger.Info("開啟編輯器開發提示，現在會顯示一些開發相關的提示訊息");
            }
        }
        [MenuItem(MenuPath, true)]
        private static bool SwitchValidate()
        {
            var disable = EWovaEditorPrefs.instance.GetBool(PrefKey, false);
            Menu.SetChecked(MenuPath, !disable);
            return true;
        }

        public static void Info(object message, UnityEngine.Object context = null)
        {
            if (IsEnabled)
                EditorLogger.InfoNoPrefix($"💡 {message}", context);
        }
        public static void Warn(object message, UnityEngine.Object context = null)
        {
            if (IsEnabled)
                EditorLogger.WarnNoPrefix($"💡 {message}", context);
        }
        public static void Err(object message, UnityEngine.Object context = null)
        {
            if (IsEnabled)
                EditorLogger.ErrNoPrefix($"💡 {message}", context);
        }
    }
}
#endif