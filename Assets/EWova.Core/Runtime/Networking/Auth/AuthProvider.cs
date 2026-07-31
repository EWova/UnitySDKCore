using Cysharp.Threading.Tasks;

using EWova.DeepLink;
using EWova.Networking;

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Security.Cryptography;

using UnityEngine;

namespace EWova.Auth
{
    /// <summary>
    /// 提供 EWova 驗證流程的基底類別。繼承時必須透過建構子傳入 <see cref="EWovaAuthConfig"/> 才能完成初始化。
    /// </summary>
    public abstract class AuthProvider : IAuthManager, IDisposable
    {
        /// <summary>
        /// 請在 RuntimeInitializeLoadType.BeforeSceneLoad 以前初始化
        /// </summary>
        public static bool EnableMockDeepLinkReceiver = true;

        public const string LaunchTicketQueryKey = "launch_ticket";

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

        internal protected readonly Logger InternalLogger;
        public ILogSource Logger => InternalLogger;
        public LogLevel LoggerLevel
        {
            get => InternalLogger.PrintLevel;
            set => InternalLogger.PrintLevel = value;
        }

        public bool IsAuthenticated => CurrentAuthState == AuthState.Authenticated || CurrentAuthState == AuthState.RefreshingToken;
        /// <summary>
        /// 目前使用的應用程式 ID，通常用於向後端換發 launch_ticket 或其他授權用途。
        /// </summary>
        public virtual string AppId { get; }
        public AuthState CurrentAuthState { get; internal set; }
        public UserProfile CurrentUser { get; internal set; }
        public event Action<AuthState> OnAuthStateChanged;

        public bool UseNativeDeepLinkReceiver { get; set; } = false;
        public Action<IAuthorizeProcess> OnAuthorizeViaBrowserStarted { get; set; }

        private bool _isProcessingDeepLink = false;
        private bool _isRefreshing = false;
        private bool _disposed = false;

        private TokenSet _currentTokens;
        private AuthorizeProcess _currentAuthorizeProcess;
        private CancellationTokenSource _renewLoopCts;

        protected readonly CancellationTokenSource _lifecycleCts = new();
        protected readonly TokenService _tokenService;
        protected readonly EWovaAuthConfig _authConfig;

        protected TokenSet CurrentTokens
        {
            get => _currentTokens;
            private set
            {
                if (ReferenceEquals(_currentTokens, value))
                    return;

                if (value == null)
                {
                    _currentTokens = null;
                    return;
                }

                _currentTokens = value;

                // 部分 AS 在 refresh_token 流程不會回傳 id_token，此時 value.Jwt 為 null，
                // 屬正常情況（見 TokenSet.Jwt 說明），應保留既有的 CurrentUser 而非清空，
                // 避免使用者資訊在每次背景續期後憑空消失。
                if (value.Jwt != null)
                {
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
        public bool IsSupportAuthorizeViaDeepLink { get; private set; } = false;

        protected AuthProvider(
            EWovaAuthConfig authConfig,
            DeepLinkHandler deepLinkHandler,
            Logger logger = null)
        {
            _authConfig = authConfig ?? throw new ArgumentNullException(nameof(authConfig));
            InternalLogger = logger ?? new Logger($"[EWova]{this.GetType().Name} ", LogLevel.Full);

            if (deepLinkHandler != null && !deepLinkHandler.IsDummy)
            {
                IsSupportAuthorizeViaDeepLink = true;
                _deepLinkHandlerActivatedEventDisposer = deepLinkHandler.ContinueWith(OnDeepLinkHandlerActivated);
                if (InternalLogger.InfoEnabled)
                    InternalLogger.Info($"成功建立 AuthProvider，支援 DeepLink Scheme: {deepLinkHandler.Scheme}，可使用 AuthorizeViaBrowser 功能。");
            }
            else
            {
                IsSupportAuthorizeViaDeepLink = false;
                if (InternalLogger.WarnEnabled)
                    InternalLogger.Warn("成功建立 AuthProvider，但不支援 DeepLink，這會導致跳轉登入無法正常工作，無法使用 AuthorizeViaBrowser 功能。");
            }

            _tokenService = new TokenService(this, _authConfig);
            if (CurrentAuthState == AuthState.Initializing)
                SetState(AuthState.Unauthenticated);
        }

        private IDisposable _deepLinkHandlerActivatedEventDisposer;
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            _deepLinkHandlerActivatedEventDisposer?.Dispose();
            _deepLinkHandlerActivatedEventDisposer = null;

            CancelAuthorizeProcess();
            StopRenewLoop();
            _lifecycleCts.Cancel();
            _lifecycleCts.Dispose();
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

            var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(_lifecycleCts.Token, cancellationToken).Token;
            return await RefreshInternalAsync(linkedToken);
        }
        public async UniTask<string> RefreshAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            if (CurrentAuthState != AuthState.Authenticated && CurrentAuthState != AuthState.RefreshingToken)
                throw new InvalidOperationException("未處於認證狀態，無法刷新 Token。");

            var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(_lifecycleCts.Token, cancellationToken).Token;
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
            OnAuthorizeViaBrowserStarted?.InvokeSafely(_currentAuthorizeProcess);

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
        /// <summary>
        /// 使用系統瀏覽器進行授權流程，並等待授權結果回傳。此方法會在授權完成或失敗後返回 <see cref="AuthorizeResult"/>，可用於非同步流程中直接取得授權結果。
        /// </summary>
        /// <param name="options">登入行為選項，若為 null 則使用預設值。</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public async UniTask<AuthorizeResult> AuthorizeViaBrowserAsync(AuthorizeViaBrowserOptions? options = null, CancellationToken cancellationToken = default)
        {
            if (!IsSupportAuthorizeViaDeepLink)
                throw new NotSupportedException("此 AuthProvider 未支援任何註冊的 DeepLinkReceiver，無法使用 AuthorizeViaBrowser 進行授權流程。");

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
        /// <summary>
        /// 在已驗證狀態下，向後端換發一張 <c>launch_ticket</c>，可用來讓 EWova App 兌換並延續目前的登入狀態。
        /// 呼叫前會先確保 access token 為最新狀態。
        /// </summary>
        /// <param name="requestAppId">要求換票的來源應用程式 id。</param>
        /// <exception cref="InvalidOperationException">尚未處於已驗證狀態時拋出。</exception>
        public async UniTask<string> CreateLaunchTicketAsync(string requestAppId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(requestAppId))
                throw new ArgumentNullException(nameof(requestAppId));

            if (CurrentAuthState != AuthState.Authenticated && CurrentAuthState != AuthState.RefreshingToken)
                throw new InvalidOperationException("未處於認證狀態，無法建立 launch ticket。");

            await RefreshAccessTokenAsync(ct);

            var rsp = await _tokenService.CreateLaunchTicketAsync(CurrentTokens.AccessToken, requestAppId, ct);
            return rsp.launchTicket;
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
            InternalHandleAuthenticationUrl(url, DeepLinkInvocationType.Runtime);
        }
        internal void InternalHandleAuthenticationUrl(
            string url,
            DeepLinkInvocationType deepLinkInvocationType)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;
            if (_isProcessingDeepLink)
            {
                if (InternalLogger.WarnEnabled)
                    InternalLogger.Warn("已有一個 Deep Link 驗證流程正在處理中，本次重複觸發的回呼已被忽略。");
                return;
            }
            _isProcessingDeepLink = true;
            HandleDeepLink(url, deepLinkInvocationType).Forget();
        }
        protected virtual void InternalOnAuthStateChanged(AuthState authState) { }

        private void CancelAuthorizeProcess()
        {
            _currentAuthorizeProcess?.Dispose();
            _currentAuthorizeProcess = null;
        }

        private async UniTaskVoid HandleDeepLink(
            string url,
            DeepLinkInvocationType deepLinkInvocationType = DeepLinkInvocationType.Runtime)
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

                var launchTicket = query[LaunchTicketQueryKey];
                if (!string.IsNullOrEmpty(launchTicket))
                {
                    if (deepLinkInvocationType == DeepLinkInvocationType.Launch)
                        return;

                    if (InternalLogger.InfoEnabled)
                        InternalLogger.Info("收到 Launch Ticket 授權請求");

                    SetState(AuthState.Authenticating);
                    var tokenSet = await _tokenService.ExchangeLaunchTicketAsync(launchTicket, _lifecycleCts.Token);
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
                        if (deepLinkInvocationType == DeepLinkInvocationType.Launch)
                        {
                            // 可能是冷啟動或其他 Provider 的 DeepLink 回調，忽略處理
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
                    var tokenSet = await _tokenService.ExchangeCodeAsync(code, authProcessing.CodeVerifier, authProcessing.Nonce, _lifecycleCts.Token);

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
                {
                    Logout();
                }
                else if (CurrentAuthState == AuthState.RefreshingToken)
                {
                    // Refresh 失敗（例如逾時、5xx）不代表 CurrentTokens 已更新成功；
                    // 只有在舊 access token 仍未過期時才可以維持已認證狀態，
                    // 否則必須反映為未認證，避免呼叫端誤以為 token 仍然有效。
                    SetState(CurrentTokens != null && !CurrentTokens.IsAccessTokenExpired
                        ? AuthState.Authenticated
                        : AuthState.Unauthenticated);
                }
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
            if (CurrentAuthState == newState)
                return;
            CurrentAuthState = newState;
            InternalOnAuthStateChanged(newState);
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
        private void OnDeepLinkHandlerActivated(DeepLinkHandler handler)
        {
            InternalHandleAuthenticationUrl(
                handler.ActiveURL,
                handler.ActiveInvocationType);
        }
    }
}
