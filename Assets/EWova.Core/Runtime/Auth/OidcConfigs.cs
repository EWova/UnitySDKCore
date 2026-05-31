using UnityEngine;

namespace EWova.Auth
{
    public static class OidcConfigs
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            string _appScheme;

            var dlConfig = DeepLink.DeepLinkConfig.LoadOrDefault();
            if (dlConfig != null)
            {
                Logger.Log($"成功載入 DeepLink.Config，App Scheme 設定為：{dlConfig.MyAppScheme}");
                _appScheme = $"{dlConfig.MyAppScheme}://callback";
            }
            else
            {
                Logger.Warn("找不到 DeepLink.Config，將使用預設的 App Scheme。請確保已在 Resources 資料夾中建立 DeeplinkConfig ScriptableObject 並設定 MyAppScheme。");
                _appScheme = "example://callback";
            }

            Prod = new OidcConfig(
                clientId: "learning-portfolio-sdk",
                baseAuthUrl: "https://auth.ewova.com",
                redirectUri: _appScheme,
                scope: "openid profile email roles organization offline_access"
            );

            Dev = new OidcConfig(
                clientId: "learning-portfolio-sdk",
                baseAuthUrl: "https://auth.ewova.dev",
                redirectUri: _appScheme,
                scope: "openid profile email roles organization offline_access"
            );
        }

        public static OidcConfig Prod;
        public static OidcConfig Dev;
        public static OidcConfig Current
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            => Dev;
#else
            => Prod;
#endif
    }
}
