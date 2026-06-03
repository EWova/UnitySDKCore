using Cysharp.Threading.Tasks;
using System.Threading;

namespace EWova.Auth
{
    public interface IAuthManager
    {
        bool IsAuthenticated { get; }
        TokenSet CurrentTokens { get; }
        AuthState CurrentAuthState { get; }
        UserProfile CurrentUser { get; }
        bool TryGetValidAccessToken(out string token);
        UniTask<string> RefreshAccessTokenAsync(CancellationToken cancellationToken = default);
        UniTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
        void Logout();
        IAuthorizeProcess AuthorizeViaBrowser(string uiLocales = null);
    }
}
