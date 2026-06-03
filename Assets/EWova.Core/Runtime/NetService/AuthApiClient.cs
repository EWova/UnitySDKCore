using EWova.Auth;

namespace EWova.NetService
{
    public partial class AuthApiClient
    {
        public AuthApiClient(IAuthManager authManager, string baseUrl)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _auth = authManager;
            _logger = new($"[{GetType().Name}] {_baseUrl}", Logger.Level.Full);
        }

        private readonly Logger _logger;
        private readonly string _baseUrl;
        private readonly IAuthManager _auth;

        public string BaseUrl => _baseUrl;
        public IAuthManager AuthManager => _auth;
        public bool IsUserAuthenticated => _auth != null && _auth.IsAuthenticated;
        public AuthState AuthState => _auth != null ? _auth.CurrentAuthState : AuthState.Unauthenticated;
        public UserProfile AuthenticatedUserProfile => IsUserAuthenticated ? _auth.CurrentUser : null;

        public bool TryGetValidAccessToken(out string token)
        {
            token = null;
            return _auth != null && _auth.TryGetValidAccessToken(out token);
        }
    }
}
