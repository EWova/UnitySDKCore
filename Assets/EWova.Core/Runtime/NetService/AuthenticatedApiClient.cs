using EWova.Auth;

namespace EWova.NetService
{
    public partial class AuthApiClient
    {
        public AuthApiClient(IAuthManager authManager, string baseUrl)
        {
            _logger = new($"[{GetType().Name}]({baseUrl}) ", Logger.Level.Full);
            _baseUrl = baseUrl;
            _auth = authManager;
        }

        public static AuthApiClient CreateEWovaAuthClient(string baseUrl)
        {
            if (EwovaAuthManager.Instance == null)
            {
                throw new System.InvalidOperationException("EWovaAuthManager instance is not initialized. Please initialize it before creating AuthenticatedApiClient.");
            }

            return new AuthApiClient(EwovaAuthManager.Instance, baseUrl);
        }

        private readonly Logger _logger;
        private readonly string _baseUrl;
        private readonly IAuthManager _auth;

        public string BaseUrl => _baseUrl;
        public IAuthManager AuthManager => _auth;
        public bool IsUserAuthenticated => _auth != null && _auth.IsAuthenticated;
        public AuthState AuthState => _auth != null ? _auth.CurrentAuthState : AuthState.Unauthenticated;
        public UserProfile AuthenticatedUserProfile => IsUserAuthenticated ? _auth.AuthenticatedUserProfile : null;

        internal string AccessToken => IsUserAuthenticated ? _auth.GetAccessToken() : null;
    }
}
