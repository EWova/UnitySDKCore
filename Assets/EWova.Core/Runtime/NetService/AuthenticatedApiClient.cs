using EWova.Auth;

namespace EWova.NetService
{
    public partial class AuthenticatedApiClient
    {
        public AuthenticatedApiClient(IAuthManager authManager, string baseUrl)
        {
            _logger = new($"[{GetType().Name}]({baseUrl}) ", Logger.Level.Full);
            _baseUrl = baseUrl;
            _auth = authManager;
        }

        public AuthenticatedApiClient(string baseUrl)
            : this(EwovaAuthManager.Instance, baseUrl)
        {
        }

        private readonly Logger _logger;
        private readonly string _baseUrl;
        private readonly IAuthManager _auth;

        public bool IsUserAuthenticated => _auth.CurrentAuthState == AuthState.Authenticated;
        public AuthState AuthState => _auth.CurrentAuthState;
        internal string AccessToken => IsUserAuthenticated ? _auth.GetAccessToken() : null;
        public UserProfile AuthenticatedUserProfile => IsUserAuthenticated ? _auth.AuthenticatedUserProfile : null;
    }
}
