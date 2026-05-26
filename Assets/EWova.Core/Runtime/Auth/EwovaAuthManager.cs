using Cysharp.Threading.Tasks;

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading;

using UnityEngine;

namespace EWova.Auth
{
    public class EwovaAuthManager : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            Instance = FindAnyObjectByType<EwovaAuthManager>();

            if (Instance == null)
            {
                var managerObj = new GameObject("[Ewova] Auth");
                Instance = managerObj.AddComponent<EwovaAuthManager>();
            }

            DontDestroyOnLoad(Instance.gameObject);

#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged += EditorApplication_playModeStateChanged;
#endif
        }

#if UNITY_EDITOR
        private static void EditorApplication_playModeStateChanged(UnityEditor.PlayModeStateChange obj)
        {
            if (obj == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                if (Instance != null)
                    Destroy(Instance.gameObject);
                Instance = null;
                UnityEditor.EditorApplication.playModeStateChanged -= EditorApplication_playModeStateChanged;
            }
        }
#endif

        public static EwovaAuthManager Instance { get; private set; }
#if UNITY_EDITOR
        public MockDeepLinkReceiver MockComponent;
#endif
        public AuthState State { get; private set; } = AuthState.Initializing;
        public event Action<AuthState> OnAuthStateChanged;
        /// <summary>
        /// 是否使用原生的 DeepLink 接收器，若為 true，則會註冊並使用原生的 DeepLink 接收器來接收 DeepLink 事件；
        /// 預設值為 false。
        /// </summary>
        public bool UseNativeDeepLinkReceiver = false;
        internal string AccessToken => _tokenSet?.AccessToken;

        private TokenService _oidcAuth;
        private readonly List<IDeepLinkReceiver> _receivers = new();
        private bool _isProcessingDeepLink = false;
        private TokenSet _tokenSet;
        private CancellationTokenSource _cts;
        private CancellationTokenSource _renewLoopCts;
        private OidcConfig CurrentOdicConfig => OidcConfigs.Current;

        private void Awake()
        {
            _cts = new CancellationTokenSource();
            _oidcAuth = new TokenService(CurrentOdicConfig);
            RegisterDefaultReceivers();

            if (State == AuthState.Initializing)
            {
                SetState(AuthState.Unauthenticated);
            }
        }

        private void RegisterDefaultReceivers()
        {
            RegisterReceiver(new DefaultDeepLinkReceiver());
            RegisterReceiver(new NativeDeepLinkReceiver());

#if UNITY_EDITOR
            if (MockComponent != null)
                RegisterReceiver(MockComponent);
#endif
        }
        public void RegisterReceiver(IDeepLinkReceiver receiver)
        {
            if (receiver == null)
                throw new ArgumentNullException(nameof(receiver));

            if (_receivers.Contains(receiver))
                return;

            _receivers.Add(receiver);

            receiver.Initialize(OnUrlReceived);

            Logger.Log($"DeepLinkReceiver registered: {receiver.Name}");
        }
        public void UnregisterReceiver(IDeepLinkReceiver receiver)
        {
            if (receiver == null)
                return;

            if (_receivers.Remove(receiver))
            {
                receiver.Dispose();

                Logger.Log($"DeepLinkReceiver unregistered: {receiver.Name}");
            }
        }

        private void OnUrlReceived(IDeepLinkReceiver receiver, string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                Logger.Warn("忽略空白的 URL");
                return;
            }

            if (_isProcessingDeepLink)
            {
                Logger.Warn($"目前正在處理另一個請求，忽略新的 URL: {url}");
                return;
            }

            if (receiver is NativeDeepLinkReceiver && !UseNativeDeepLinkReceiver)
            {
                Logger.Warn($"收到來自 NativeDeepLinkReceiver 的 URL，但 UseNativeDeepLinkReceiver 設定為 false，將忽略此 URL: {url}");
                return;
            }

            Logger.Log($"DeepLinkReceiver: '{receiver.GetType().Name}'. URL: '{url}'");

            try
            {
                _isProcessingDeepLink = true;
                HandleDeepLink(url);
            }
            catch (Exception ex)
            {
                _isProcessingDeepLink = false;
                Logger.Err($"DeepLink handling failed: {ex}");
                SetState(AuthState.Unauthenticated);
            }
        }
        private void HandleDeepLink(string url)
        {
            // scheme 驗證：確保收到的 URL 是以預期的 redirect URI 開頭，避免處理不相關或潛在惡意的 URL。
            if (!url.StartsWith(CurrentOdicConfig.RedirectUri, StringComparison.OrdinalIgnoreCase))
            {
                Logger.Warn($"忽略不相關的 URL，預期以 {CurrentOdicConfig.RedirectUri} 開頭，但收到: {url}");
                _isProcessingDeepLink = false;
                return;
            }

            try
            {
                var uri = new Uri(url);

                NameValueCollection query = HttpUtility.ParseQueryString(uri.Query);

                // 2. 優先處理錯誤回傳
                var error = query["error"];
                if (!string.IsNullOrEmpty(error))
                {
                    Logger.Err($"OIDC 認證錯誤回傳: {error}");
                    SetState(AuthState.Unauthenticated);
                    _isProcessingDeepLink = false;
                    return;
                }

                // 3. 流程 A：Ticket-based 驗證流程
                var launchTicket = query["launch_ticket"];
                if (!string.IsNullOrEmpty(launchTicket))
                {
                    Logger.Log($"接收到 LaunchTicket 參數的 URL: {url}");

                    var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, this.GetCancellationTokenOnDestroy()).Token;
                    ProcessOidcAuthFromLaunchTicket(launchTicket, linkedToken).Forget();
                    return;
                }

                Logger.Err($"收到的 URL 中缺少必要的參數，無法進行認證流程。URL: {url}");
                SetState(AuthState.Unauthenticated);
                _isProcessingDeepLink = false;
            }
            catch (Exception ex)
            {
                Logger.Err($"處理 URL 時發生例外: {ex.Message}");
                UnityEngine.Debug.LogException(ex);
                SetState(AuthState.Unauthenticated);
                _isProcessingDeepLink = false;
            }
        }

        private async UniTaskVoid ProcessOidcAuthFromLaunchTicket(string launchTicket, CancellationToken token = default)
        {
            SetState(AuthState.Authenticating);

            Logger.Log("LaunchTicket 開始進行 OIDC 認證流程...");
            try
            {
                _tokenSet = await _oidcAuth.ExchangeLaunchTicketAsync(launchTicket, token);

                // 寫入狀態前 double-check 取消狀態，防止與 Logout 發生 Race Condition
                token.ThrowIfCancellationRequested();

                SetState(AuthState.Authenticated);
                Logger.Log("LaunchTicket OIDC 認證流程完成，使用者已成功登入。");
            }
            catch (RefreshTokenGrantException)
            {
                Logger.Err("LaunchTicket Token 無效或已過期，請重新啟動認證流程。");
                SetState(AuthState.Unauthenticated);
            }
            catch (OperationCanceledException)
            {
                Logger.Warn("LaunchTicket 交換過程已取消");
                if (State == AuthState.Authenticating)
                    SetState(AuthState.Unauthenticated);
            }
            catch (Exception ex)
            {
                Logger.Err($"LaunchTicket 進行 OIDC 認證流程時發生例外: {ex.Message}");
                UnityEngine.Debug.LogException(ex);
                SetState(AuthState.Unauthenticated);
            }
            finally
            {
                _isProcessingDeepLink = false;
            }
        }
        private async UniTask<string> RefreshInternalAsync(CancellationToken cancellationToken)
        {
            if (_tokenSet == null || string.IsNullOrEmpty(_tokenSet.RefreshToken) || _tokenSet.IsRefreshTokenExpired)
            {
                Logger.Warn("Token 不存在或 Refresh Token 已過期，直接執行登出。");
                Logout();
                return null;
            }

            if (!_tokenSet.IsAccessTokenExpired)
            {
                return _tokenSet.AccessToken;
            }

            var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
            var linkedToken = linkedTokenSource.Token;

            SetState(AuthState.RefreshingToken);
            try
            {
                var refreshed = await _oidcAuth.RefreshAsync(_tokenSet.RefreshToken, linkedToken);

                linkedToken.ThrowIfCancellationRequested();

                _tokenSet = refreshed;
                SetState(AuthState.Authenticated);
                return _tokenSet.AccessToken;
            }
            catch (RefreshTokenGrantException)
            {
                Logger.Err("Refresh token 無效或已過期，使用者需要重新登入。");
                Logout();
                return null;
            }
            catch (OperationCanceledException)
            {
                Logger.Warn("刷新 token 過程已取消");
                if (State == AuthState.RefreshingToken) SetState(AuthState.Authenticated);
                return _tokenSet?.AccessToken;
            }
            catch (Exception ex)
            {
                Logger.Err($"刷新 token 時發生例外: {ex.Message}");
                if (State == AuthState.RefreshingToken) SetState(AuthState.Authenticated);
                return _tokenSet?.AccessToken;
            }
        }
        private async UniTaskVoid StartTokenRenewLoop(CancellationToken token)
        {
            Logger.Log("開始 Token 自動續期背景監聽。");

            // 每 15 秒檢查一次本地狀態，避免頻繁呼叫 DateTime.UtcNow 或是過度消耗 CPU
            var checkInterval = TimeSpan.FromSeconds(15);

            try
            {
                while (!token.IsCancellationRequested)
                {
                    await UniTask.Delay(checkInterval, cancellationToken: token);

                    // 只有在已登入狀態下才需要驗證與續期
                    if (State == AuthState.Authenticated && _tokenSet != null)
                    {
                        // 提前 30 秒續期，確保在 access token 過期前完成續期流程，避免因網路延遲等因素導致的過期問題
                        if (_tokenSet.IsAccessTokenExpired)
                        {
                            Logger.Log("偵測到 Access Token 即將過期，觸發自動續期流程...");
                            await RefreshInternalAsync(token);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Log("Token 自動續期背景監聽已安全停止。");
            }
            catch (Exception ex)
            {
                Logger.Err($"自動續期迴圈發生未預期的錯誤: {ex.Message}");
            }
        }

        public void Logout()
        {
            StopRenewLoop();

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            _tokenSet = null;
            _isProcessingDeepLink = false;
            SetState(AuthState.Unauthenticated);
        }

        private void StopRenewLoop()
        {
            if (_renewLoopCts != null)
            {
                _renewLoopCts.Cancel();
                _renewLoopCts.Dispose();
                _renewLoopCts = null;
            }
        }

        private void SetState(AuthState newState)
        {
            if (State == newState)
                return;
            State = newState;
            OnAuthStateChanged?.Invoke(newState);

            if (newState == AuthState.Authenticated)
            {
                StopRenewLoop();
                _renewLoopCts = new CancellationTokenSource();
                StartTokenRenewLoop(_renewLoopCts.Token).Forget();
            }
            else if (newState == AuthState.Unauthenticated)
            {
                StopRenewLoop();
            }
        }

        private void OnDestroy()
        {
            StopRenewLoop();

            _cts?.Cancel();
            _cts?.Dispose();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= EditorApplication_playModeStateChanged;
#endif

            foreach (var r in _receivers)
            {
                try
                {
                    r.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Err($"Receiver dispose failed: {ex}");
                }
            }

            _receivers.Clear();
        }
    }
}