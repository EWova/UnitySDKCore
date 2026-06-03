using System.Collections.Generic;

using UnityEngine;

namespace EWova.Auth
{
    public static class EWovaAuthConfigSettings
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            string _appScheme;

            var dlConfig = DeepLink.DeepLinkConfig.LoadOrDefault();
            if (dlConfig != null)
            {
                if (EwovaAuthManager.Logger.InfoEnabled)
                    EwovaAuthManager.Logger.Info($"成功載入 DeepLink.Config，App Scheme 設定為：{dlConfig.MyAppScheme}");
                _appScheme = $"{dlConfig.MyAppScheme}";
            }
            else
            {
                if (EwovaAuthManager.Logger.WarnEnabled)
                    EwovaAuthManager.Logger.Warn("找不到 DeepLink.Config，將使用預設的 App Scheme。請確保已在 Resources 資料夾中建立 DeeplinkConfig ScriptableObject 並設定 MyAppScheme。");
                _appScheme = "app-not-configured";
            }

            Prod = new EWovaAuthConfig(
                clientId: "learning-portfolio-sdk",
                baseAuthUrl: "https://auth.ewova.com",
                customUriScheme: _appScheme,
                scopes: new List<string> { "openid", "profile", "email", "roles", "organization", "offline_access" }
            );

            Dev = new EWovaAuthConfig(
                clientId: "learning-portfolio-sdk",
                baseAuthUrl: "https://auth.ewova.dev",
                customUriScheme: _appScheme,
                scopes: new List<string> { "openid", "profile", "email", "roles", "organization", "offline_access" }
            );
        }

        public static EWovaAuthConfig Prod;
        public static EWovaAuthConfig Dev;
        public static EWovaAuthConfig Current
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            => Dev;
#else
            => Prod;
#endif
    }
}
