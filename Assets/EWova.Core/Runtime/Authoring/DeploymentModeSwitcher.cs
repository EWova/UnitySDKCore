#if UNITY_EDITOR
using UnityEditor;

namespace EWova.Authoring
{
    public static class DeploymentModeSwitcher
    {
        private const string MenuPathProduction = "EWova/Editor/Deployment Mode/Production";
        private const string MenuPathDevelopment = "EWova/Editor/Deployment Mode/Development";
        private const string PrefKey = "Env_EditorDeploymentMode";

        [MenuItem(MenuPathProduction, false, 1)]
        private static void SwitchToProduction()
        {
            SetDeploymentMode(DeploymentMode.Production);
        }

        [MenuItem(MenuPathProduction, true)]
        private static bool SwitchToProductionValidate()
        {
            Menu.SetChecked(MenuPathProduction, Environment.DeploymentMode == DeploymentMode.Production);
            return true;
        }

        [MenuItem(MenuPathDevelopment, false, 2)]
        private static void SwitchToDevelopment()
        {
            SetDeploymentMode(DeploymentMode.Development);
        }

        [MenuItem(MenuPathDevelopment, true)]
        private static bool SwitchToDevelopmentValidate()
        {
            Menu.SetChecked(MenuPathDevelopment, Environment.DeploymentMode == DeploymentMode.Development);
            return true;
        }

        private static void SetDeploymentMode(DeploymentMode mode)
        {
            if (Environment.DeploymentMode == mode) return;

            EWovaEditorPrefs.instance.SetInt(PrefKey, (int)mode);

            EditorLogger.Info($"編輯器部屬環境已切換到 {mode}");
        }
    }
}
#endif