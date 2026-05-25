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

        [ContextMenu("Invoke DeepLink With IDeepLinkReceiver")]
        public void InvokeDeepLinkWithReceiver()
        {
            if (string.IsNullOrEmpty(_deeplink))
            {
                Logger.Err("錯誤的 Deeplink，請先取得 launch ticket 並填入 _deeplink 欄位");
                return;
            }

            _mockCallback?.Invoke(this, _deeplink);
        }

        [ContextMenu("Invoke DeepLink With Application.OpenURL")]
        public void InvokeDeepLinkWithOpenURL()
        {
            if (string.IsNullOrEmpty(_deeplink))
            {
                Logger.Err("錯誤的 Deeplink，請先取得 launch ticket 並填入 _deeplink 欄位");
                return;
            }

            // 從 Application.OpenURL 觸發的 DeepLink 會由 DefaultDeepLinkReceiver 處理，繞過 MockDeepLinkReceiver 的 callback
            Application.OpenURL(_deeplink);
        }

        [Header("Request")]
        [TextArea]
        public string AccessToken;
        public string AppId;
        [Header("Response")]
        public LaunchTicketResponse res;

        [ContextMenu("Get Test URL")]
        public void GetTestUrl()
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

            RequestLaunchTicket().Forget();
        }


        private async UniTaskVoid RequestLaunchTicket()
        {
            res = new LaunchTicketResponse();

            // get reflect object 
            var _oidcAuth = EwovaAuthManager.Instance.GetType()
                .GetField("_oidcAuth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(EwovaAuthManager.Instance) as TokenService;

            try
            {
                res = await _oidcAuth.CreateLaunchTicketAsync(AccessToken, AppId);
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

            _deeplink = res.deepLink;
        }
    }
}
