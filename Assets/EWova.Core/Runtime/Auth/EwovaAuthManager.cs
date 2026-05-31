using Cysharp.Threading.Tasks;

using Newtonsoft.Json.Linq;

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

using UnityEngine;

namespace EWova.Auth
{
    public class EwovaAuthManager : MonoBehaviour, IAuthManager
    {
        private class AuthorizeProcess
        {
            public AuthorizeProcess()
            {
                CreatedAt = DateTimeOffset.UtcNow;
                CodeVerifier = PkceHelper.GenerateCodeVerifier();
                CodeChallenge = PkceHelper.GenerateCodeChallenge(CodeVerifier);
                State = PkceHelper.GenerateState();
                Nonce = PkceHelper.GenerateNonce();
            }

            public readonly DateTimeOffset CreatedAt;
            public readonly string CodeVerifier;
            public readonly string CodeChallenge;
            public readonly string State;
            public readonly string Nonce;

            /// <summary>
            /// 此授權流程是否已過期（超過 10 分鐘未完成）。過期的流程應該被丟棄，並要求使用者重新啟動認證流程。
            /// </summary>
            public bool IsExpired => DateTimeOffset.UtcNow - CreatedAt > TimeSpan.FromMinutes(10);
        }

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

        public static EwovaAuthManager Instance 
        {
            get 
            {
                if(!Application.isPlaying)
                    throw new InvalidOperationException("EwovaAuthManager 取得失敗，請在 Play 模式下使用。");
                return _instance;
            }
            private set => _instance = value;
        }
        private static EwovaAuthManager _instance;
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

        internal TokenService _tokenService { get; private set; }
        private readonly List<IDeepLinkReceiver> _receivers = new();

        private bool _isProcessingDeepLink = false;
        private bool _isRefreshing = false; // 防止併發刷新
        private AuthorizeProcess _currentAuthorizeProcess;

        private CancellationTokenSource _cts;
        private CancellationTokenSource _renewLoopCts;
        private OidcConfig CurrentOdicConfig => OidcConfigs.Current;

        private void Awake()
        {
            _cts = new CancellationTokenSource();
            _tokenService = new TokenService(CurrentOdicConfig);
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

        /// <summary>
        /// 獲取 OIDC 認證 URL，使用者可以透過這個 URL 進行登入，完成後會由 Deep Link 接收器接收回傳的認證結果。
        /// </summary>
        public string GetAuthorizeUrl(string uiLocales = null)
        {
            _currentAuthorizeProcess = new AuthorizeProcess();

            var authorizeUrl = CurrentOdicConfig.BuildAuthorizeUrl(
                _currentAuthorizeProcess.CodeChallenge,
                _currentAuthorizeProcess.State,
                _currentAuthorizeProcess.Nonce,
                uiLocales);

            return authorizeUrl;
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
            HandleDeepLink(url).Forget();
        }

        private async UniTaskVoid HandleDeepLink(string url)
        {
            if (_isProcessingDeepLink)
            {
                Logger.Warn($"目前正在處理另一個請求，忽略新的 URL: {url}");
                return;
            }

            _isProcessingDeepLink = true;

            try
            {
                var uri = new Uri(url);
                var redirectUri = new Uri(CurrentOdicConfig.RedirectUri);

                if (!string.Equals(uri.Scheme, redirectUri.Scheme, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Warn($"忽略不相關的 URL，預期以 {CurrentOdicConfig.RedirectUri} 開頭，但收到: {url}");
                    return;
                }

                var query = HttpUtility.ParseQueryString(uri.Query);

                var error = query["error"];
                if (!string.IsNullOrEmpty(error))
                {
                    Logger.Err($"OIDC 認證錯誤回傳: {error}");
                    SetState(AuthState.Unauthenticated);
                    return;
                }

                var launchTicket = query["launch_ticket"];
                if (!string.IsNullOrEmpty(launchTicket))
                {
                    Logger.Log($"接收到 LaunchTicket 參數的 URL: {url}");

                    SetState(AuthState.Authenticating);

                    var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(
                        _cts.Token,
                        this.GetCancellationTokenOnDestroy()
                    ).Token;

                    var tokenSet = await _tokenService.ExchangeLaunchTicketAsync(
                        launchTicket,
                        linkedToken
                    );

                    CurrentTokenSet = tokenSet;
                    SetState(AuthState.Authenticated);
                    Logger.Log("LaunchTicket 交換成功，已成功驗證使用者身份。");
                    return;
                }

                var code = query["code"];
                var state = query["state"];
                if (!string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(state))
                {
                    Logger.Log($"接收到 Authorization Code 參數的 URL: {url}");

                    if (_currentAuthorizeProcess == null || _currentAuthorizeProcess.IsExpired)
                    {
                        Logger.Warn("收到的授權回應已過期，請重新啟動認證流程。");
                        SetState(AuthState.Unauthenticated);
                        return;
                    }

                    var thisState = _currentAuthorizeProcess.State;
                    var thisCodeVerifier = _currentAuthorizeProcess.CodeVerifier;
                    var thisNonce = _currentAuthorizeProcess.Nonce;

                    // state 安全比較（避免簡單 string 比對）
                    if (!CryptographicOperations.FixedTimeEquals(
                            Encoding.UTF8.GetBytes(state),
                            Encoding.UTF8.GetBytes(thisState)))
                    {
                        Logger.Warn($"state 不匹配，可能 CSRF 或流程錯亂。預期: {thisState}，實際: {state}");
                        SetState(AuthState.Unauthenticated);
                        return;
                    }

                    SetState(AuthState.Authenticating);

                    var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(
                        _cts.Token,
                        this.GetCancellationTokenOnDestroy()
                    ).Token;

                    var tokenSet = await _tokenService.ExchangeCodeAsync(
                        code,
                        thisCodeVerifier,
                        thisNonce,
                        linkedToken
                    );

                    CurrentTokenSet = tokenSet;
                    Logger.Log("Authorization Code 交換成功，已成功驗證使用者身份。");
                    SetState(AuthState.Authenticated);
                    return;
                }

                Logger.Err($"收到的 URL 缺少必要參數 (code/state/launch_ticket)，無法進行認證流程: {url}");
                SetState(AuthState.Unauthenticated);
            }
            catch (TokenEndpointException ex)
            {
                Logger.Err($"Token 交換失敗: {ex.Error} - {ex.Message}");
                SetState(AuthState.Unauthenticated);
            }
            catch (RefreshTokenExpiredException)
            {
                Logger.Err("提供的 LaunchTicket 無效或已過期，請重新啟動認證流程。");
                SetState(AuthState.Unauthenticated);
            }
            catch (OperationCanceledException)
            {
                Logger.Warn("處理 URL 的過程已取消");
                SetState(AuthState.Unauthenticated);
            }
            catch (Exception ex)
            {
                Logger.Err($"處理 URL 時發生例外: {ex.Message}");
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
                var refreshed = await _tokenService.RefreshAsync(CurrentTokenSet.RefreshToken, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                CurrentTokenSet = refreshed;
                SetState(AuthState.Authenticated);
                UpdateUserProfile();

                return CurrentTokenSet.AccessToken;
            }
            catch (RefreshTokenExpiredException)
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