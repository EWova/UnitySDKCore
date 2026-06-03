using Cysharp.Threading.Tasks;

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

using UnityEngine;

namespace EWova.Auth
{
    public class EwovaAuthManager : MonoBehaviour, IAuthManager
    {
        private sealed class AuthorizeProcess : IAuthorizeProcess
        {
            private bool _disposed;
            private bool _cancelled;
            private readonly CancellationTokenSource _cts = new();

            public AuthorizeProcess(IAuthManager authManager)
            {
                AuthManager = authManager ?? throw new ArgumentNullException(nameof(authManager));
                CreatedAt = DateTimeOffset.UtcNow;
                CodeVerifier = PkceHelper.GenerateCodeVerifier();
                CodeChallenge = PkceHelper.GenerateCodeChallenge(CodeVerifier);
                State = PkceHelper.GenerateState();
                Nonce = PkceHelper.GenerateNonce();
            }

            public IAuthManager AuthManager { get; }
            public readonly DateTimeOffset CreatedAt;
            internal readonly string CodeVerifier;
            internal readonly string CodeChallenge;
            internal readonly string State;
            internal readonly string Nonce;
            public bool IsCompleted { get; private set; }
            public bool IsCancelled => _cancelled;
            public bool IsExpired => DateTimeOffset.UtcNow - CreatedAt > TimeSpan.FromMinutes(10);
            internal CancellationToken CancellationToken => _cts.Token;
            public event Action OnCancelled;
            public event Action OnCompleted;

            internal void Complete()
            {
                ThrowIfDisposed();
                if (_cancelled || IsCompleted) return;
                IsCompleted = true;
                OnCompleted?.Invoke();
            }

            public void Cancel()
            {
                ThrowIfDisposed();
                if (_cancelled || IsCompleted) return;
                _cancelled = true;
                _cts.Cancel();
                OnCancelled?.Invoke();
            }

            public void Dispose()
            {
                if (_disposed) return;
                if (!_cancelled && !IsCompleted) Cancel();
                _disposed = true;
                _cts.Dispose();
                OnCancelled = null;
                OnCompleted = null;
            }

            private void ThrowIfDisposed()
            {
                if (_disposed) throw new ObjectDisposedException(nameof(AuthorizeProcess));
            }
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
                if (Instance != null) Destroy(Instance.gameObject);
                Instance = null;
                UnityEditor.EditorApplication.playModeStateChanged -= EditorApplication_playModeStateChanged;
            }
        }
#endif
        public static EwovaAuthManager Instance
        {
            get
            {
                if (!Application.isPlaying)
                    throw new InvalidOperationException("EwovaAuthManager 取得失敗，請在 Play 模式下使用。");
                return _instance;
            }
            private set => _instance = value;
        }
        private static EwovaAuthManager _instance;
        public readonly static Logger Logger = new Logger("[EwovaAuthManager] ", Logger.Level.Warn | Logger.Level.Error);

        public bool IsAuthenticated => CurrentAuthState == AuthState.Authenticated || CurrentAuthState == AuthState.RefreshingToken;
        public AuthState CurrentAuthState { get; internal set; }
        public TokenSet CurrentTokens { get; internal set; }
        public UserProfile CurrentUser { get; internal set; }
        public event Action<AuthState> OnAuthStateChanged;
        public bool UseNativeDeepLinkReceiver { get; set; } = false;

        internal TokenService TokenService { get; private set; }
#if UNITY_EDITOR
        public MockDeepLinkReceiver MockComponent;
#endif
        private readonly List<IDeepLinkReceiver> _receivers = new();
        private bool _isProcessingDeepLink = false;
        private bool _isRefreshing = false;
        private AuthorizeProcess _currentAuthorizeProcess;
        private CancellationTokenSource _cts;
        private CancellationTokenSource _renewLoopCts;
        private EWovaAuthConfig CurrentOdicConfig => EWovaAuthConfigSettings.Current;

        private void Awake()
        {
            _cts = new CancellationTokenSource();
            TokenService = new TokenService(CurrentOdicConfig);
            RegisterDefaultReceivers();
            if (CurrentAuthState == AuthState.Initializing) SetState(AuthState.Unauthenticated);
        }

        private void OnDestroy()
        {
            CancelAuthorizeProcess();
            StopRenewLoop();
            _cts?.Cancel();
            _cts?.Dispose();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= EditorApplication_playModeStateChanged;
#endif
            foreach (var r in _receivers)
            {
                try { r.Dispose(); }
                catch (Exception ex)
                {
                    if (Logger.ErrorEnabled)
                        Logger.Err("Receiver dispose");

                    UnityEngine.Debug.LogException(ex);
                }
            }
            _receivers.Clear();
        }

        private void CancelAuthorizeProcess()
        {
            _currentAuthorizeProcess?.Dispose();
            _currentAuthorizeProcess = null;
        }

        private void RegisterDefaultReceivers()
        {
            RegisterReceiver(new DefaultDeepLinkReceiver());
            if (UseNativeDeepLinkReceiver) RegisterReceiver(new NativeDeepLinkReceiver());
#if UNITY_EDITOR
            if (MockComponent != null) RegisterReceiver(MockComponent);
#endif
        }

        public void RegisterReceiver(IDeepLinkReceiver receiver)
        {
            if (receiver == null) throw new ArgumentNullException(nameof(receiver));
            if (_receivers.Contains(receiver)) return;
            _receivers.Add(receiver);
            receiver.Initialize(OnUrlReceived);
            if (Logger.InfoEnabled)
                Logger.Info($"DeepLinkReceiver registered: {receiver.Name}");
        }

        public void UnregisterReceiver(IDeepLinkReceiver receiver)
        {
            if (receiver == null) return;
            if (_receivers.Remove(receiver))
            {
                receiver.Dispose();
                if (Logger.InfoEnabled)
                    Logger.Info($"DeepLinkReceiver unregistered: {receiver.Name}");
            }
        }

        public bool TryGetValidAccessToken(out string accessToken)
        {
            if ((CurrentAuthState == AuthState.Authenticated || CurrentAuthState == AuthState.RefreshingToken) && CurrentTokens != null && !CurrentTokens.IsAccessTokenExpired)
            {
                accessToken = CurrentTokens.AccessToken;
                return true;
            }
            accessToken = null;
            return false;
        }

        public async UniTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            if (CurrentAuthState != AuthState.Authenticated && CurrentAuthState != AuthState.RefreshingToken)
                throw new InvalidOperationException("未處於認證狀態，無法獲取 Access Token。");

            if (CurrentTokens != null && !CurrentTokens.IsAccessTokenExpired)
                return CurrentTokens.AccessToken;

            var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken).Token;
            return await RefreshInternalAsync(linkedToken);
        }

        public async UniTask<string> RefreshAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            if (CurrentAuthState != AuthState.Authenticated && CurrentAuthState != AuthState.RefreshingToken)
                throw new InvalidOperationException("未處於認證狀態，無法刷新 Token。");

            var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken).Token;
            return await RefreshInternalAsync(linkedToken);
        }

        public IAuthorizeProcess AuthorizeViaBrowser(string uiLocales = null)
        {
            if (CurrentAuthState == AuthState.Authenticated || CurrentAuthState == AuthState.RefreshingToken)
                throw new InvalidOperationException("已經處於認證狀態，無需再次授權。");

            CancelAuthorizeProcess();
            _currentAuthorizeProcess = new AuthorizeProcess(this);

            var authorizeUrl = CurrentOdicConfig.BuildAuthorizeUrl(
                _currentAuthorizeProcess.CodeChallenge,
                _currentAuthorizeProcess.State,
                _currentAuthorizeProcess.Nonce,
                uiLocales);

            if (Logger.InfoEnabled)
                Logger.Info($"開始授權流程 State: {_currentAuthorizeProcess.State}, Url: {authorizeUrl}");
            Application.OpenURL(authorizeUrl);
            return _currentAuthorizeProcess;
        }

        public void Logout()
        {
            CancelAuthorizeProcess();
            StopRenewLoop();
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            CurrentTokens = null;
            _isProcessingDeepLink = false;
            _isRefreshing = false;
            SetState(AuthState.Unauthenticated);
        }

        private void OnUrlReceived(IDeepLinkReceiver receiver, string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            if (_isProcessingDeepLink) return;
            if (receiver is NativeDeepLinkReceiver && !UseNativeDeepLinkReceiver) return;
            if (Logger.InfoEnabled)
                Logger.Info($"DeepLink Received by '{receiver.GetType().Name}'. URL: '{url}'");
            _isProcessingDeepLink = true;
            HandleDeepLink(url).Forget();
        }

        private async UniTaskVoid HandleDeepLink(string url)
        {
            try
            {
                var uri = new Uri(url);
                var redirectUri = new Uri(CurrentOdicConfig.RedirectUri);

                if (!string.Equals(uri.Scheme, redirectUri.Scheme, StringComparison.OrdinalIgnoreCase))
                    return;

                var query = HttpUtility.ParseQueryString(uri.Query);
                var error = query["error"];
                if (!string.IsNullOrEmpty(error))
                    throw new TokenEndpointException(400, error, $"認證錯誤回傳: {error}");

                var launchTicket = query["launch_ticket"];
                if (!string.IsNullOrEmpty(launchTicket))
                {
                    SetState(AuthState.Authenticating);
                    var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, this.GetCancellationTokenOnDestroy()).Token;
                    var tokenSet = await TokenService.ExchangeLaunchTicketAsync(launchTicket, linkedToken);
                    CurrentTokens = tokenSet;
                    SetState(AuthState.Authenticated);
                    return;
                }

                var code = query["code"];
                var state = query["state"];
                if (!string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(state))
                {
                    using var authProcessing = _currentAuthorizeProcess;
                    if (authProcessing == null || authProcessing.IsCancelled) return;
                    if (authProcessing.IsExpired) throw new TimeoutException("授權流程已過期。");

                    if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(state), Encoding.UTF8.GetBytes(authProcessing.State)))
                        throw new System.Security.SecurityException("State 不匹配，可能遭受 CSRF 攻擊。");

                    SetState(AuthState.Authenticating);
                    var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, authProcessing.CancellationToken, this.GetCancellationTokenOnDestroy()).Token;
                    var tokenSet = await TokenService.ExchangeCodeAsync(code, authProcessing.CodeVerifier, authProcessing.Nonce, linkedToken);

                    if (authProcessing.IsCancelled) return;

                    CurrentTokens = tokenSet;
                    if (ReferenceEquals(authProcessing, _currentAuthorizeProcess)) _currentAuthorizeProcess = null;
                    SetState(AuthState.Authenticated);
                    authProcessing.Complete();
                    return;
                }

                throw new ArgumentException("收到的 URL 缺少必要參數 (code/state/launch_ticket)。");
            }
            catch (Exception ex)
            {
                Logger.Err($"處理 URL 時發生錯誤: {ex.Message}");
                if (ex is not OperationCanceledException) UnityEngine.Debug.LogException(ex);
                CancelAuthorizeProcess();
                SetState(AuthState.Unauthenticated);
            }
            finally
            {
                _isProcessingDeepLink = false;
            }
        }

        private async UniTask<string> RefreshInternalAsync(CancellationToken cancellationToken)
        {
            if (_isRefreshing)
            {
                await UniTask.WaitWhile(() => _isRefreshing, cancellationToken: cancellationToken);
                if (CurrentTokens == null || CurrentTokens.IsAccessTokenExpired)
                    throw new RefreshTokenExpiredException("併發刷新完成，但未獲得有效 Token。");
                return CurrentTokens.AccessToken;
            }

            if (CurrentTokens == null || string.IsNullOrEmpty(CurrentTokens.RefreshToken) || CurrentTokens.IsRefreshTokenExpired)
            {
                Logout();
                throw new RefreshTokenExpiredException("Token 不存在或 Refresh Token 已過期。");
            }

            if (!CurrentTokens.IsAccessTokenExpired && CurrentAuthState != AuthState.RefreshingToken)
            {
                return CurrentTokens.AccessToken;
            }

            _isRefreshing = true;
            SetState(AuthState.RefreshingToken);

            try
            {
                var refreshed = await TokenService.RefreshTokenAsync(CurrentTokens.RefreshToken, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                CurrentTokens = refreshed;
                SetState(AuthState.Authenticated);
                UpdateUserProfile();
                return CurrentTokens.AccessToken;
            }
            catch (Exception ex)
            {
                if (ex is RefreshTokenExpiredException) Logout();
                else if (CurrentAuthState == AuthState.RefreshingToken) SetState(AuthState.Authenticated);
                throw;
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private async UniTaskVoid StartTokenRenewLoop(CancellationToken token)
        {
            var checkInterval = TimeSpan.FromSeconds(15);
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await UniTask.Delay(checkInterval, cancellationToken: token);
                    if (CurrentAuthState == AuthState.Authenticated && CurrentTokens != null)
                    {
                        if (CurrentTokens.IsAccessTokenExpired || CurrentTokens.ExpiresIn < TimeSpan.FromSeconds(60))
                        {
                            try { await RefreshInternalAsync(token); }
                            catch (Exception ex) { Logger.Err($"自動續期失敗: {ex.Message}"); }
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
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
                CurrentUser = null;
            }
        }

        private void UpdateUserProfile()
        {
            if (CurrentAuthState != AuthState.Authenticated || CurrentTokens == null)
            {
                CurrentUser = null;
                return;
            }
            CurrentUser = UserProfile.FromJwt(CurrentTokens.Jwt, DateTimeOffset.UtcNow);
        }
    }
}