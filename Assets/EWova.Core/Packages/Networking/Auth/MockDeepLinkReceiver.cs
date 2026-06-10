using Cysharp.Threading.Tasks;

using System;

using UnityEngine;

namespace EWova.Auth
{
    /// <summary>
    /// 模擬 DeepLink 接收器，提供在 Editor 模式下測試使用
    /// </summary>
    public class MockDeepLinkReceiver : MonoBehaviour
    {
        [SerializeField] private string _deeplink = "";

        private void Awake()
        {

        }

        [ContextMenu("Get TestURL and Invoke DeepLink With IDeepLinkReceiver")]
        public void InvokeDeepLinkWithReceiver()
        {
            if (string.IsNullOrEmpty(_deeplink))
            {
                RequestLaunchTicket((str) =>
                {
                    _deeplink = str;
                    EwovaAuthManager.Instance.HandleAuthenticationUrl(str);
                }).Forget();
            }
            else
            {
                EwovaAuthManager.Instance.HandleAuthenticationUrl(_deeplink);
            }

        }

        [ContextMenu("Get TestURL and Invoke DeepLink With Application.OpenURL")]
        public void InvokeDeepLinkWithOpenURL()
        {
            if (string.IsNullOrEmpty(_deeplink))
            {
                RequestLaunchTicket((str) =>
                {
                    _deeplink = str;
                    Application.OpenURL(str);
                }).Forget();
            }
            else
            {
                // 從 Application.OpenURL 觸發的 DeepLink 會由 DefaultDeepLinkReceiver 處理，繞過 MockDeepLinkReceiver 的 callback
                Application.OpenURL(_deeplink);
            }
        }

        [Header("Request")]
        [TextArea]
        public string AccessToken;
        public string AppId;
        [Header("Response")]
        public LaunchTicketResponse res;

        [ContextMenu("Just Get TestURL")]
        public void GetTestUrl()
        {
            RequestLaunchTicket((str) => _deeplink = str).Forget();
        }

        private async UniTaskVoid RequestLaunchTicket(Action<string> action = null)
        {
            bool error = false;

            if (string.IsNullOrEmpty(AccessToken))
            {
                if (EwovaAuthManager.InternalLogger.ErrorEnabled)
                    EwovaAuthManager.InternalLogger.Err("錯誤的 AccessToken，可以到 admin.ewova 找 cookie oidc.user:https://auth.ewova.dev:admin-portal 取得 access_token");
                error = true;
            }

            if (string.IsNullOrEmpty(AppId))
            {
                if (EwovaAuthManager.InternalLogger.ErrorEnabled)
                    EwovaAuthManager.InternalLogger.Err("錯誤的 AppId，請選擇你要測試的軟體 https://admin.ewova.dev/apps");
                error = true;
            }

            if (error)
                return;

            res = new LaunchTicketResponse();

            // get reflect object 
            TokenService tokenService = typeof(EwovaAuthManager)
                .GetField("_tokenService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                .GetValue(EwovaAuthManager.Instance) as TokenService;
            try
            {
                res = await tokenService.CreateLaunchTicketAsync(AccessToken, AppId);
                action?.Invoke(res.deepLink);
            }
            catch (TokenEndpointException ex)
            {
                if (EwovaAuthManager.InternalLogger.ErrorEnabled)
                    EwovaAuthManager.InternalLogger.Err($"取得 launch ticket TokenEndpointException 失敗: {ex}");

                UnityEngine.Debug.LogException(ex);
                return;
            }
            catch (Exception ex)
            {
                if (EwovaAuthManager.InternalLogger.ErrorEnabled)
                    EwovaAuthManager.InternalLogger.Err($"取得 launch ticket 失敗: {ex}");

                UnityEngine.Debug.LogException(ex);
                return;
            }
        }
        [ContextMenu("Try Get Launch Ticket")]
        public void TryGetLaunchTicket()
        {
            IAuthManager auth = EwovaAuthManager.Instance;
            if (auth.CurrentAuthState != AuthState.Authenticated)
            {
                Debug.LogWarning("User is not authenticated.");
                return;
            }
            EwovaAuthManager.Instance.LaunchEWovaAppWithLoginAsync(AppId).Forget();
        }

        public bool IsSupport(RuntimePlatform runtimePlatform)
        {
            if (!EwovaAuthManager.EnableMockDeepLinkReceiver)
                return false;

            return runtimePlatform is
                RuntimePlatform.WindowsEditor or
                RuntimePlatform.OSXEditor or
                RuntimePlatform.LinuxEditor;
        }
    }
}
