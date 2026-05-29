using Cysharp.Threading.Tasks;

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading;

using UnityEngine;

namespace EWova.Auth
{
    public class EwovaAuthManager : MonoBehaviour, IAuthManager
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
        public AuthState CurrentAuthState { get; internal set; }
        public TokenSet CurrentTokenSet { get; internal set; }
        public UserProfile AuthenticatedUserProfile { get; internal set; }

        public event Action<AuthState> OnAuthStateChanged;

        /// <summary>
        /// 是否使用原生的 DeepLink 接收器，若為 true，則會註冊並使用原生的 DeepLink 接收器來接收 DeepLink 事件；
        /// 預設值為 false。
        /// </summary>
        public bool UseNativeDeepLinkReceiver = false;

        private TokenService _oidcAuth;
        private readonly List<IDeepLinkReceiver> _receivers = new();

        private bool _isProcessingDeepLink = false;
        private bool _isRefreshing = false; // 防止併發刷新

        private CancellationTokenSource _cts;
        private CancellationTokenSource _renewLoopCts;
        private OidcConfig CurrentOdicConfig => OidcConfigs.Current;

        private void Awake()
        {
            _cts = new CancellationTokenSource();
            _oidcAuth = new TokenService(CurrentOdicConfig);
            RegisterDefaultReceivers();

            if (CurrentAuthState == AuthState.Initializing)
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
            if (receiver == null) return;

            if (_receivers.Remove(receiver))
            {
                receiver.Dispose();
                Logger.Log($"DeepLinkReceiver unregistered: {receiver.Name}");
            }
        }

        /// <summary>
        /// 同步獲取 Access Token。若過期會回傳 null 並警告。
        /// </summary>
        string IAuthManager.GetAccessToken()
        {
            if (CurrentAuthState != AuthState.Authenticated && CurrentAuthState != AuthState.RefreshingToken)
            {
                Logger.Warn("嘗試獲取 Access Token，但目前未處於已認證狀態。");
                return null;
            }
            if (CurrentTokenSet != null && !CurrentTokenSet.IsAccessTokenExpired)
            {
                return CurrentTokenSet.AccessToken;
            }

            Logger.Warn("Access Token 已過期，請呼叫 RefreshAccessToken 或使用 GetAccessTokenAsync()。");
            return null;
        }

        /// <summary>
        /// 非同步獲取 Access Token。若過期會自動等待刷新完成再回傳。
        /// </summary>
        public async UniTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            if (CurrentAuthState != AuthState.Authenticated && CurrentAuthState != AuthState.RefreshingToken)
                return null;

            if (CurrentTokenSet != null && !CurrentTokenSet.IsAccessTokenExpired)
            {
                return CurrentTokenSet.AccessToken;
            }

            var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken).Token;
            return await RefreshInternalAsync(linkedToken);
        }

        /// <summary>
        /// 觸發手動刷新 Token (Fire-and-Forget)
        /// </summary>
        public void RefreshAccessToken()
        {
            RefreshAccessTokenAsync().Forget();
        }

        /// <summary>
        /// 觸發手動刷新 Token (可等待)
        /// </summary>
        public async UniTask<bool> RefreshAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            if (CurrentAuthState != AuthState.Authenticated && CurrentAuthState != AuthState.RefreshingToken)
            {
                Logger.Warn("未處於認證狀態，無法刷新 Token。");
                return false;
            }

            var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken).Token;
            var newToken = await RefreshInternalAsync(linkedToken);
            return !string.IsNullOrEmpty(newToken);
        }

        public void ClearTokenSet()
        {
            StopRenewLoop();

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            CurrentTokenSet = null;
            _isProcessingDeepLink = false;
            _isRefreshing = false;
            SetState(AuthState.Unauthenticated);
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

                var error = query["error"];
                if (!string.IsNullOrEmpty(error))
                {
                    Logger.Err($"OIDC 認證錯誤回傳: {error}");
                    SetState(AuthState.Unauthenticated);
                    _isProcessingDeepLink = false;
                    return;
                }

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
                CurrentTokenSet = await _oidcAuth.ExchangeLaunchTicketAsync(launchTicket, token);
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
                if (CurrentAuthState == AuthState.Authenticating)
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
            // 防止併發：如果已經在刷新中，則等待當前的刷新任務完成，直接拿新的結果
            if (_isRefreshing)
            {
                await UniTask.WaitWhile(() => _isRefreshing, cancellationToken: cancellationToken);
                return CurrentTokenSet?.AccessToken;
            }

            if (CurrentTokenSet == null || string.IsNullOrEmpty(CurrentTokenSet.RefreshToken) || CurrentTokenSet.IsRefreshTokenExpired)
            {
                Logger.Warn("Token 不存在或 Refresh Token 已過期，直接執行登出。");
                ClearTokenSet();
                return null;
            }

            // 再次檢查確保真的需要刷新
            if (!CurrentTokenSet.IsAccessTokenExpired && CurrentAuthState != AuthState.RefreshingToken)
            {
                return CurrentTokenSet.AccessToken;
            }

            _isRefreshing = true;
            SetState(AuthState.RefreshingToken);

            try
            {
                var refreshed = await _oidcAuth.RefreshAsync(CurrentTokenSet.RefreshToken, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                CurrentTokenSet = refreshed;
                SetState(AuthState.Authenticated);
                UpdateUserProfile();

                return CurrentTokenSet.AccessToken;
            }
            catch (RefreshTokenGrantException)
            {
                Logger.Err("Refresh token 無效或已過期，使用者需要重新登入。");
                ClearTokenSet();
                return null;
            }
            catch (OperationCanceledException)
            {
                Logger.Warn("刷新 token 過程已取消");
                if (CurrentAuthState == AuthState.RefreshingToken)
                    SetState(AuthState.Authenticated); // 發生取消時，退回上一個狀態避免卡死
                return CurrentTokenSet?.AccessToken;
            }
            catch (Exception ex)
            {
                Logger.Err($"刷新 token 時發生例外: {ex.Message}");
                if (CurrentAuthState == AuthState.RefreshingToken)
                    SetState(AuthState.Authenticated);
                return CurrentTokenSet?.AccessToken;
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private async UniTaskVoid StartTokenRenewLoop(CancellationToken token)
        {
            Logger.Log("開始 Token 自動續期背景監聽。");
            var checkInterval = TimeSpan.FromSeconds(15);

            try
            {
                while (!token.IsCancellationRequested)
                {
                    await UniTask.Delay(checkInterval, cancellationToken: token);

                    if (CurrentAuthState == AuthState.Authenticated && CurrentTokenSet != null)
                    {
                        if (CurrentTokenSet.IsAccessTokenExpired)
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
            if (CurrentAuthState == newState) return;

            CurrentAuthState = newState;
            OnAuthStateChanged?.Invoke(newState);

            if (newState == AuthState.Authenticated)
            {
                UpdateUserProfile();
                StopRenewLoop();
                _renewLoopCts = new CancellationTokenSource();
                StartTokenRenewLoop(_renewLoopCts.Token).Forget();
            }
            else if (newState == AuthState.Unauthenticated)
            {
                StopRenewLoop();
                AuthenticatedUserProfile = null;
            }
        }

        private void UpdateUserProfile()
        {
            if (CurrentAuthState != AuthState.Authenticated || CurrentTokenSet == null)
            {
                AuthenticatedUserProfile = null;
                return;
            }

            AuthenticatedUserProfile = UserProfile.FromJwt(CurrentTokenSet.Jwt, DateTimeOffset.UtcNow);
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