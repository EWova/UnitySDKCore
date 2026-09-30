using Cysharp.Threading.Tasks;

using EWova.Auth;

using System;
using System.Threading;

namespace EWova.Networking
{
    public partial class AuthApiClient : IDisposable
    {
        public AuthApiClient(IAuthManager authManager, string baseUrl, Logger logger = null)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _auth = authManager;
            _logger = logger ?? new($"[EWova]{GetType().Name} ", LogLevel.Error);

            if (_logger.InfoEnabled)
                _logger.Info($"Created ApiClient with BaseUrl: {_baseUrl}");
        }

        protected readonly Logger _logger;
        protected readonly string _baseUrl;
        protected readonly IAuthManager _auth;

        /// <summary>
        /// 所有透過此 Client 發出的 HTTP 請求逾時秒數（對應 UnityWebRequest.timeout）。
        /// 若伺服器接受連線後不回應（例如防火牆靜默丟包），沒有逾時將導致請求無限期掛起，
        /// 對於背景 token 刷新等流程會造成永久卡死，因此預設必須有一個合理的逾時值。
        /// </summary>
        public static int DefaultRequestTimeoutSeconds { get; set; } = 30;

        public LogLevel LoggerLevel
        {
            get => _logger.PrintLevel;
            set => _logger.PrintLevel = value;
        }

        public string BaseUrl => _baseUrl;
        public IAuthManager AuthManager => _auth;
        public bool IsUserAuthenticated => _auth != null && _auth.IsAuthenticated;
        public AuthState AuthState => _auth != null ? _auth.CurrentAuthState : AuthState.Unauthenticated;
        public UserIdentity? AuthenticatedUserProfile => _auth?.CurrentUser;

        public bool TryGetValidAccessToken(out string token)
        {
            token = null;
            return _auth != null && _auth.TryGetValidAccessToken(out token);
        }

        protected readonly CancellationTokenSource _disposeCts = new();

        private int _disposeState = 0;
        // 0 = alive, 1 = disposing, 2 = disposed
        public bool IsDisposed => _disposeState == 2;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposeState, 1) != 0)
                return;

            GC.SuppressFinalize(this);
            _ = DisposeCoreAsync();
        }
        protected virtual UniTask OnDisposeAsync()
        {
            return UniTask.CompletedTask;
        }
        private async UniTask DisposeCoreAsync()
        {
            try
            {
                await UniTask.SwitchToMainThread();
            }
            catch { }

            try
            {
                await OnDisposeAsync().SuppressCancellationThrow();
            }
            catch (Exception ex)
            {
                if (_logger.ErrorEnabled)
                    _logger.Err($"OnDisposeAsync failed: {ex}");
            }

            try
            {
                _disposeCts.Cancel();
            }
            catch { }

            _disposeCts.Dispose();

            Volatile.Write(ref _disposeState, 2);
        }
        protected void ThrowIfDisposed()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(AuthApiClient));
        }
    }
}