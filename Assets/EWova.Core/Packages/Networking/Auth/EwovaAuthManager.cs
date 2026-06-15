using Cysharp.Threading.Tasks;

using EWova.DeepLink;
using EWova.Networking;

using System;
using System.Text;
using System.Threading;
using System.Security.Cryptography;

using UnityEngine;

namespace EWova.Auth
{
    [DefaultExecutionOrder(-1000)]
    public partial class EwovaAuthManager : MonoBehaviour, IAuthManager
    {
        /// <summary>
        /// 請在 RuntimeInitializeLoadType.BeforeSceneLoad 以前初始化
        /// </summary>
        public static bool EnableMockDeepLinkReceiver = true;

        private sealed class AuthorizeProcess : IAuthorizeProcess
        {
            private bool _disposed;
            private readonly CancellationTokenSource _cts = new();

            internal AuthorizeProcess(IAuthManager authManager, Action<AuthorizeResult> onCompleted = null)
            {
                AuthManager = authManager ?? throw new ArgumentNullException(nameof(authManager));
                CreatedAt = DateTimeOffset.UtcNow;
                CodeVerifier = PkceHelper.GenerateCodeVerifier();
                CodeChallenge = PkceHelper.GenerateCodeChallenge(CodeVerifier);
                State = PkceHelper.GenerateState();
                Nonce = PkceHelper.GenerateNonce();
                if (onCompleted != null)
                    OnCompleted += onCompleted;
            }

            public IAuthManager AuthManager { get; }
            public AuthorizeResult? Result { get; private set; }
            public bool IsCompleted { get; private set; }
            public bool IsExpired => DateTimeOffset.UtcNow - CreatedAt > TimeSpan.FromMinutes(10);
            public event Action<AuthorizeResult> OnCompleted;

            public readonly DateTimeOffset CreatedAt;
            internal readonly string CodeVerifier;
            internal readonly string CodeChallenge;
            internal readonly string State;
            internal readonly string Nonce;
            internal CancellationToken CancellationToken => _cts.Token;

            internal void Complete()
                => Finish(AuthorizeResult.Ok());
            internal void Fail(string errorMessage)
                => Finish(AuthorizeResult.Fail(errorMessage));
            internal void SetException(Exception ex)
                => Finish(AuthorizeResult.FromException(ex));

            public void Cancel()
            {
                if (_disposed || IsCompleted) return;

                _cts.Cancel();
                Finish(AuthorizeResult.Cancel());
            }
            private void Finish(AuthorizeResult result)
            {
                if (_disposed || IsCompleted) return;

                Result = result;
                IsCompleted = true;
                OnCompleted?.Invoke(result);
            }
            public void Dispose()
            {
                if (_disposed) return;

                if (!IsCompleted)
                    Cancel();

                _disposed = true;
                _cts.Dispose();
                OnCompleted = null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            Instance = FindAnyObjectByType<EwovaAuthManager>();
            if (Instance == null)
            {
                var managerObj = new GameObject("[Ewova]AuthManager ");
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
        internal readonly static Logger InternalLogger = new("[Ewova]AuthManager ", LogLevel.Full);
        public static ILogSource Logger => InternalLogger;
        public static LogLevel LoggerLevel
        {
            get => InternalLogger.PrintLevel;
            set => InternalLogger.PrintLevel = value;
        }

        public bool IsAuthenticated => CurrentAuthState == AuthState.Authenticated || CurrentAuthState == AuthState.RefreshingToken;
        public AuthState CurrentAuthState { get; internal set; }
        public UserProfile CurrentUser { get; internal set; }
        public event Action<AuthState> OnAuthStateChanged;

        public bool UseNativeDeepLinkReceiver { get; set; } = false;

        private bool _isProcessingDeepLink = false;
        private bool _isRefreshing = false;

        private TokenSet _currentTokens;
        private AuthorizeProcess _currentAuthorizeProcess;
        private CancellationTokenSource _renewLoopCts;
        private DeepLinkHandler _deepLinkHandler;
        private EWovaAuthConfig _authConfig;
        private TokenService _tokenService;
        private TokenSet CurrentTokens
        {
            get => _currentTokens;
            set
            {
                if (ReferenceEquals(_currentTokens, value))
                    return;

                if (value == null)
                {
                    _currentTokens = null;
                    return;
                }

                _currentTokens = value;

                try
                {
                    CurrentUser = UserProfile.FromJwt(value.Jwt, DateTimeOffset.UtcNow);
                }
                catch (Exception ex)
                {
                    if (InternalLogger.ErrorEnabled)
                        InternalLogger.Err($"JWT parse failed. {ex.Message}");

                    CurrentUser = null;
                }

                if (InternalLogger.InfoEnabled)
                {
                    var userInfo = CurrentUser == null
                        ? "user=null"
                        : $"userId={CurrentUser.Id}, userName={CurrentUser.Name}";

                    string refreshInfo;

                    if (value.RefreshToken == null)
                    {
                        refreshInfo = "refreshToken=none";
                    }
                    else if (!value.RefreshExpiresIn.HasValue)
                    {
                        refreshInfo = "refreshToken=exists, refreshExpire=unknown";
                    }
                    else
                    {
                        refreshInfo = $"refreshToken=exists, refreshExpire={(int)value.RefreshExpiresIn.Value.TotalSeconds}s";
                    }

                    InternalLogger.Info(
                        $"Token issued. {userInfo}, accessExpire={(int)value.ExpiresIn.TotalSeconds}s, {refreshInfo}");
                }
            }
        }
        public bool IsSupportAuthorizeViaDeepLink
            => IsDeepLinkHandlerAvailable;

        private bool IsDeepLinkHandlerAvailable 
            => DeepLinkHandler.IsSupported;

        private void Awake()
        {
            string appScheme;
            DeepLinkHandler deepLinkHandler = null;

            deepLinkHandler = DeepLinkHandler.Default;
            appScheme = deepLinkHandler.Scheme;

            if (InternalLogger.InfoEnabled)
                InternalLogger.Info($"成功載入 Resource/{DeepLinkConfig.ResourceName}，AppScheme 設定為：{appScheme}");

            if (!IsDeepLinkHandlerAvailable)
            {
                if (InternalLogger.WarnEnabled)
                    InternalLogger.Warn("當前平台不支援 DeepLink，這會導致跳轉登入無法正常工作，無法使用 AuthorizeViaBrowser 功能。");
            }

            deepLinkHandler.ContinueWith(OnDeepLinkHandlerActivated);
            _deepLinkHandler = deepLinkHandler;

            _authConfig = EWovaAuthConfigFactory.Create(appScheme, Environment.DeploymentMode);
            _tokenService = new TokenService(this, _authConfig);
            if (CurrentAuthState == AuthState.Initializing)
                SetState(AuthState.Unauthenticated);
        }
        private void OnDestroy()
        {
            if (_deepLinkHandler != null)
            {
                _deepLinkHandler.Remove(OnDeepLinkHandlerActivated);
                _deepLinkHandler = null;
            }
            CancelAuthorizeProcess();
            StopRenewLoop();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= EditorApplication_playModeStateChanged;
#endif
        }

        public bool TryGetValidAccessToken(out string accessToken)
        {
            if (IsAuthenticated && CurrentTokens != null && !CurrentTokens.IsAccessTokenExpired)
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

            var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken, cancellationToken).Token;
            return await RefreshInternalAsync(linkedToken);
        }
        public async UniTask<string> RefreshAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            if (CurrentAuthState != AuthState.Authenticated && CurrentAuthState != AuthState.RefreshingToken)
                throw new InvalidOperationException("未處於認證狀態，無法刷新 Token。");

            var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken, cancellationToken).Token;
            return await RefreshInternalAsync(linkedToken);
        }
        /// <exception cref="PlatformNotSupportedException">當前平台不支援任何註冊的 DeepLinkReceiver，無法進行授權流程。</exception>
        /// <exception cref="InvalidOperationException"></exception>
        public IAuthorizeProcess AuthorizeViaBrowser(AuthorizeViaBrowserOptions? options = null, Action<AuthorizeResult> onCompleted = null)
        {
            if (!IsSupportAuthorizeViaDeepLink)
                throw new PlatformNotSupportedException("當前平台不支援任何註冊的 DeepLinkReceiver，無法進行授權流程。");

            if (CurrentAuthState == AuthState.Authenticated || CurrentAuthState == AuthState.RefreshingToken)
                throw new InvalidOperationException("已經處於認證狀態，無需再次授權。");

            CancelAuthorizeProcess();
            _currentAuthorizeProcess = new AuthorizeProcess(this, onCompleted);

            AuthorizeViaBrowserOptions effectiveOptions = options ?? AuthorizeViaBrowserOptions.Default;

            string prompt = null;

            if (effectiveOptions.ConsentRequired)
                prompt = "consent";

            if (effectiveOptions.LoginBehavior != LoginBehavior.Standard)
            {
                if (prompt != null)
                    prompt += " ";
                prompt += effectiveOptions.LoginBehavior switch
                {
                    LoginBehavior.ForceLogin => "login",
                    LoginBehavior.Silent => "none",
                    LoginBehavior.SelectAccount => "select_account",
                    _ => throw new ArgumentOutOfRangeException()
                };
            }

            string uiLocales = null;
            if (effectiveOptions.UiLocales != null)
                uiLocales = string.Join(" ", effectiveOptions.UiLocales);

            var authorizeUrl = AuthRequestBuilder.BuildAuthorizeUrl(
                _authConfig,
                _currentAuthorizeProcess.CodeChallenge,
                _currentAuthorizeProcess.State,
                _currentAuthorizeProcess.Nonce,
                prompt,
                uiLocales);

            if (InternalLogger.InfoEnabled)
                InternalLogger.Info($"開始授權流程，等待回調... State: {_currentAuthorizeProcess.State}");

            Application.OpenURL(authorizeUrl);
            return _currentAuthorizeProcess;
        }
        public async UniTask<AuthorizeResult> AuthorizeViaBrowserAsync(AuthorizeViaBrowserOptions? options = null, CancellationToken cancellationToken = default)
        {
            var tcs = new UniTaskCompletionSource<AuthorizeResult>();
            IAuthorizeProcess process = null;

            using CancellationTokenRegistration registration = cancellationToken.Register(() =>
            {
                process?.Dispose();
                tcs.TrySetCanceled(cancellationToken);
            });

            try
            {
                process = AuthorizeViaBrowser(options, result => tcs.TrySetResult(result));
                return await tcs.Task;
            }
            finally
            {
                process?.Dispose();
            }
        }
        public async UniTask LaunchEWovaAppWithLoginAsync(string requestAppId, Guid? worldId = null, int? spaceId = null, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(requestAppId))
                throw new ArgumentNullException(nameof(requestAppId));

            if (CurrentAuthState != AuthState.Authenticated && CurrentAuthState != AuthState.RefreshingToken)
                throw new InvalidOperationException("未處於認證狀態，無法啟動 EWova App。");

            await RefreshAccessTokenAsync(ct);

            string launchTicket;
            try
            {
                var rsp = await _tokenService.CreateLaunchTicketAsync(CurrentTokens.AccessToken, requestAppId, ct);
                launchTicket = rsp.launchTicket;
            }
            catch (ApiException ex)
            {
                InternalLogger.Warn($"無法取得 launch ticket，無法啟動 EWova App: {ex.Message}");
                return;
            }
            catch (Exception ex)
            {
                InternalLogger.Err($"取得 launch ticket 時發生錯誤: {ex.Message}");
                return;
            }

            var launchUrl = AuthRequestBuilder.BuildLaunchEWovaAppUrlWithLaunchTick(
                EWovaApp.Scheme,
                requestAppId,
                null,
                worldId,
                spaceId);

            if (InternalLogger.InfoEnabled)
                InternalLogger.Info($"嘗試啟動 EWova App，URL: {launchUrl}");

            Application.OpenURL(launchUrl);
        }
        public void Logout()
        {
            CancelAuthorizeProcess();
            StopRenewLoop();
            if (CurrentTokens != null)
            {
                if (InternalLogger.InfoEnabled)
                    InternalLogger.Info("使用者登出。");
                CurrentTokens = null;
            }
            _isProcessingDeepLink = false;
            _isRefreshing = false;
            SetState(AuthState.Unauthenticated);
        }
        public void HandleAuthenticationUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            if (_isProcessingDeepLink)
                return;
            _isProcessingDeepLink = true;

            HandleDeepLink(url).Forget();
        }

        private void CancelAuthorizeProcess()
        {
            _currentAuthorizeProcess?.Dispose();
            _currentAuthorizeProcess = null;
        }

        private async UniTaskVoid HandleDeepLink(string url)
        {
            if (IsAuthenticated)
            {
                if (InternalLogger.WarnEnabled)
                    InternalLogger.Warn($"收到 URL 但當前已處於認證狀態，忽略處理。URL: {url}");
                _isProcessingDeepLink = false;
                return;
            }

            var authProcessing = _currentAuthorizeProcess;
            void HandleAuthFailure(string errorMessage)
            {
                if (InternalLogger.ErrorEnabled)
                    InternalLogger.Err($"處理 URL 時發生錯誤: {errorMessage}");

                if (authProcessing != null && !authProcessing.IsCompleted)
                {
                    authProcessing.Fail(errorMessage);
                    if (ReferenceEquals(authProcessing, _currentAuthorizeProcess))
                        _currentAuthorizeProcess = null;
                }
                else
                {
                    CancelAuthorizeProcess();
                }

                SetState(AuthState.Unauthenticated);
            }

            try
            {
                var uri = new Uri(url);
                var redirectUri = new Uri(_authConfig.RedirectUri);

                if (!string.Equals(uri.Scheme, redirectUri.Scheme, StringComparison.OrdinalIgnoreCase))
                {
                    if (InternalLogger.InfoEnabled)
                        InternalLogger.Info($"收到 URL 的 Scheme 不匹配，忽略處理。Received: {uri.Scheme}, Expected: {redirectUri.Scheme}");
                    return;
                }

                var query = HttpUtility.ParseQueryString(uri.Query);

                var error = query["error"];
                if (!string.IsNullOrEmpty(error))
                {
                    HandleAuthFailure($"認證錯誤回傳: {error}");
                    return;
                }

                var launchTicket = query["launch_ticket"];
                if (!string.IsNullOrEmpty(launchTicket))
                {
                    if (InternalLogger.InfoEnabled)
                        InternalLogger.Info("收到 Launch Ticket 授權請求");

                    SetState(AuthState.Authenticating);
                    var tokenSet = await _tokenService.ExchangeLaunchTicketAsync(launchTicket, destroyCancellationToken);
                    CurrentTokens = tokenSet;
                    if (InternalLogger.InfoEnabled)
                        InternalLogger.Info("使用者成功登入。");

                    // 清除另一個可能存在的授權流程
                    if (authProcessing != null && !authProcessing.IsCompleted)
                    {
                        authProcessing.Cancel();
                        if (ReferenceEquals(authProcessing, _currentAuthorizeProcess))
                            _currentAuthorizeProcess = null;
                    }

                    SetState(AuthState.Authenticated);
                    return;
                }

                var code = query["code"];
                var state = query["state"];

                if (!string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(state))
                {
                    if (authProcessing == null)
                    {
                        if (Time.realtimeSinceStartup < 1f)
                        {
                            // 這一段可能是 Windows 冷啟動所帶入的 URL，沒有被清理掉的授權流程
                            if (InternalLogger.WarnEnabled)
                                InternalLogger.Warn("收到 URL 但沒有有效的授權流程正在進行，嘗試清理過期的授權流程並重新處理 URL。");
                            return;
                        }
                        HandleAuthFailure("沒有有效的授權流程正在進行，無法處理授權回調。");
                        return;
                    }

                    if (authProcessing.IsCompleted)
                    {
                        HandleAuthFailure("授權流程已結束 (完成或取消)，授權回調已失效。");
                        return;
                    }
                    if (authProcessing.IsExpired)
                    {
                        HandleAuthFailure("授權流程已過期。");
                        return;
                    }

                    if (InternalLogger.InfoEnabled)
                        InternalLogger.Info($"收到授權回調 State: {state}");

                    if (!CryptographicOperations.FixedTimeEquals(
                        Encoding.UTF8.GetBytes(state),
                        Encoding.UTF8.GetBytes(authProcessing.State)))
                    {
                        HandleAuthFailure("State 參數不匹配，可能存在安全風險，已拒絕處理授權回調。");
                        return;
                    }

                    SetState(AuthState.Authenticating);
                    var tokenSet = await _tokenService.ExchangeCodeAsync(code, authProcessing.CodeVerifier, authProcessing.Nonce, destroyCancellationToken);

                    if (authProcessing.IsCompleted)
                        return;

                    CurrentTokens = tokenSet;
                    authProcessing.Complete();
                    if (InternalLogger.InfoEnabled)
                        InternalLogger.Info("使用者成功登入。");

                    if (ReferenceEquals(authProcessing, _currentAuthorizeProcess))
                        _currentAuthorizeProcess = null;

                    SetState(AuthState.Authenticated);
                    return;
                }

                // 忽略其他 URL，因為可能是其他功能的 DeepLink 回調
                if (InternalLogger.InfoEnabled)
                    InternalLogger.Info($"收到 URL 但無必要參數，忽略處理。URL: {url}");
                // HandleAuthFailure("收到的 URL 缺少必要參數 (code/state/launch_ticket)。");
            }
            catch (OperationCanceledException)
            {
                authProcessing?.Cancel();
                if (ReferenceEquals(authProcessing, _currentAuthorizeProcess))
                    _currentAuthorizeProcess = null;

                SetState(AuthState.Unauthenticated);
            }
            catch (Exception ex)
            {
                if (InternalLogger.ErrorEnabled)
                    InternalLogger.Err($"處理 URL 時發生未預期錯誤: {ex.Message}");

                UnityEngine.Debug.LogException(ex);

                if (authProcessing != null && !authProcessing.IsCompleted)
                {
                    authProcessing.SetException(ex);
                    if (ReferenceEquals(authProcessing, _currentAuthorizeProcess))
                        _currentAuthorizeProcess = null;
                }
                else
                {
                    CancelAuthorizeProcess();
                }

                SetState(AuthState.Unauthenticated);
            }
            finally
            {
                _isProcessingDeepLink = false;
                authProcessing?.Dispose();
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

            if (CurrentTokens == null)
            {
                Logout();
                throw new RefreshTokenExpiredException("Token 不存在。");
            }
            if (CurrentTokens.RefreshToken == null)
            {
                Logout();
                throw new RefreshTokenExpiredException("無 Refresh Token 無法續期。");
            }
            if (CurrentTokens.IsRefreshTokenExpired)
            {
                Logout();
                throw new RefreshTokenExpiredException("Refresh Token 已過期");
            }

            if (!CurrentTokens.IsAccessTokenExpired && CurrentAuthState != AuthState.RefreshingToken)
            {
                return CurrentTokens.AccessToken;
            }

            _isRefreshing = true;
            SetState(AuthState.RefreshingToken);

            try
            {
                var refreshed = await _tokenService.RefreshTokenAsync(CurrentTokens.RefreshToken, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                CurrentTokens = refreshed;
                SetState(AuthState.Authenticated);
                return CurrentTokens.AccessToken;
            }
            catch (Exception ex)
            {
                if (ex is RefreshTokenExpiredException)
                    Logout();
                else if (CurrentAuthState == AuthState.RefreshingToken)
                    SetState(AuthState.Authenticated);
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
                            try
                            {
                                await RefreshInternalAsync(token);
                                if (InternalLogger.InfoEnabled)
                                    InternalLogger.Info("Token 自動續期");
                            }
                            catch (Exception ex)
                            {
                                if (InternalLogger.ErrorEnabled)
                                    InternalLogger.Err($"Token 自動續期失敗: {ex.Message}");

                                Debug.LogException(ex);
                            }
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
        private void OnDeepLinkHandlerActivated(DeepLinkHandler url)
        {
            HandleAuthenticationUrl(url.ActiveURL);
        }
    }
}