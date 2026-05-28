using System;

using EWova.Auth;

namespace EWova.NetService
{
    public partial class AuthenticatedApiClient
    {
        public AuthenticatedApiClient(string baseUrl)
        {
            _logger = new($"[{GetType().Name}]({baseUrl}) ", Logger.Level.Full);
            _baseUrl = baseUrl;
        }
        public readonly Logger _logger;
        private readonly string _baseUrl;

        public static bool IsUserAuthenticated => EwovaAuthManager.Instance.State == AuthState.Authenticated;
        public static AuthState AuthState => EwovaAuthManager.Instance.State;
        internal static TokenSet AuthenticatedTokenSet => IsUserAuthenticated ? EwovaAuthManager.Instance.TokenSet : null;
        public static UserProfile AuthenticatedUserProfile => IsUserAuthenticated ? EwovaAuthManager.Instance.AuthenticatedUserProfile : null;
    }
}
