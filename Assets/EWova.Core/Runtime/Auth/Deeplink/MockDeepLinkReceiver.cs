using Cysharp.Threading.Tasks;

using System;

using UnityEngine;

namespace EWova.Auth
{
    /// <summary>
    /// 模擬 DeepLink 接收器，提供在 Editor 模式下測試使用
    /// </summary>
    public class MockDeepLinkReceiver : MonoBehaviour, IDeepLinkReceiver
    {
        public string Name => "Mock";

        [SerializeField] private string _deeplink = "";

        private Action<IDeepLinkReceiver, string> _mockCallback;
        void IDeepLinkReceiver.Initialize(Action<IDeepLinkReceiver, string> onUrlReceived) => _mockCallback = onUrlReceived;
        void IDisposable.Dispose() => _mockCallback = null;

        [ContextMenu("Get TestURL and Invoke DeepLink With IDeepLinkReceiver")]
        public void InvokeDeepLinkWithReceiver()
        {
            if (string.IsNullOrEmpty(_deeplink))
            {
                RequestLaunchTicket((str) =>
                {
                    _deeplink = str;
                    _mockCallback?.Invoke(this, _deeplink);
                }).Forget();
            }
            else
            {
                _mockCallback?.Invoke(this, _deeplink);
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
                Logger.Err("錯誤的 AccessToken，可以到 admin.ewova 找 cookie oidc.user:https://auth.ewova.dev:admin-portal 取得 access_token");
                error = true;
            }

            if (string.IsNullOrEmpty(AppId))
            {
                Logger.Err("錯誤的 AppId，請選擇你要測試的軟體 https://admin.ewova.dev/apps");
                error = true;
            }

            if (error)
                return;

            res = new LaunchTicketResponse();

            // get reflect object 
            var _oidcAuth = EwovaAuthManager.Instance._tokenService;
            try
            {
                res = await _oidcAuth.CreateLaunchTicketAsync(AccessToken, AppId);
                action?.Invoke(res.deepLink);
            }
            catch (TokenEndpointException ex)
            {
                Logger.Err($"取得 launch ticket TokenEndpointException 失敗: {ex.Error} - {ex.Message}");
                return;
            }
            catch (Exception ex)
            {
                Logger.Err($"取得 launch ticket 失敗: {ex}");
                return;
            }
        }
    }
}
