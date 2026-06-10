using Cysharp.Threading.Tasks;

using Newtonsoft.Json.Linq;
using System;
using System.Threading;

using EWova.Networking;

namespace EWova.Auth
{
    [Serializable]
    public class LaunchTicketResponse
    {
        public string launchTicket;
        public string deepLink;
    }

    public class TokenService
    {
        private readonly EWovaAuthConfig m_config;
        private readonly AuthApiClient m_apiClient;

        public TokenService(IAuthManager authManager, EWovaAuthConfig config)
        {
            m_config = config ?? throw new ArgumentNullException(nameof(config));
            m_apiClient = new AuthApiClient(authManager, m_config.BaseAuthUrl);
            m_apiClient.LoggerLevel = LogLevel.Error;
        }

        public async UniTask<TokenSet> ExchangeCodeAsync(
            string code,
            string codeVerifier,
            string nonce,
            CancellationToken cancellationToken = default)
        {
            var response = await m_apiClient.Post<TokenResponse>(
                endpoint: m_config.TokenEndpoint,
                body: AuthRequestBuilder.BuildExchangeCodeBody(m_config, code, codeVerifier),
                contentType: "application/x-www-form-urlencoded",
                ct: cancellationToken);

            var tokenSet = TokenSet.FromResponse(response);
            var payload = tokenSet.Jwt.Payload;

            if (payload.Nonce != nonce)
                throw new TokenEndpointException(400, "invalid_nonce", "Nonce mismatch");

            if (payload.Audience != m_config.ClientId)
                throw new TokenEndpointException(400, "invalid_audience", "Audience mismatch");

            if (payload.Issuer != m_config.Issuer)
                throw new TokenEndpointException(400, "invalid_issuer", "Issuer mismatch");

            var expiry = DateTimeOffset.FromUnixTimeSeconds(payload.Expiry).UtcDateTime;
            if (DateTime.UtcNow >= expiry)
                throw new TokenEndpointException(400, "id_token_expired", "Token expired");

            return tokenSet;
        }

        public async UniTask<TokenSet> ExchangeLaunchTicketAsync(
            string launchTicket,
            CancellationToken cancellationToken = default)
        {
            var response = await m_apiClient.Post<TokenResponse>(
                endpoint: m_config.TokenEndpoint,
                body: AuthRequestBuilder.BuildExchangeLaunchTicketBody(m_config, launchTicket),
                acceptType: "application/json",
                contentType: "application/x-www-form-urlencoded",
                ct: cancellationToken);

            return TokenSet.FromResponse(response);
        }

        public async UniTask<LaunchTicketResponse> CreateLaunchTicketAsync(
            string accessToken,
            string appId,
            CancellationToken ct = default)
        {
            return await m_apiClient.Send<LaunchTicketResponse>(
                urlOrEndpoint: m_config.LaunchTicketEndpoint,
                method: "POST",
                acceptType: "application/json",
                body: AuthRequestBuilder.BuildCreateLaunchTicketJsonBody(appId),
                contentType: "application/json",
                isAbsoluteUrl: false,
                postProcRequestTask: (reqTask) =>
                {
                    reqTask.Headers["Authorization"] = $"Bearer {accessToken}";
                },
                ct: ct);
        }

        public async UniTask<TokenSet> RefreshTokenAsync(
            string refreshToken,
            CancellationToken cancellationToken = default)
        {
            var body = AuthRequestBuilder.BuildRefreshTokenBody(m_config, refreshToken);
            const int maxRetries = 3;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    var response = await m_apiClient.Post<TokenResponse>(
                        endpoint: m_config.TokenEndpoint,
                        body: body,
                        acceptType: "application/json",
                        contentType: "application/x-www-form-urlencoded",
                        ct: cancellationToken);

                    return TokenSet.FromResponse(response);
                }
                catch (Exception ex)
                {
                    if (ex is RefreshTokenExpiredException)
                        throw;
                    if (attempt == maxRetries - 1)
                        throw;
                    await UniTask.Delay(TimeSpan.FromSeconds(1 << attempt), cancellationToken: cancellationToken);
                }
            }
            throw new TokenEndpointException(500, "refresh_failed", "Refresh token failed after retries");
        }
    }

    public class TokenEndpointException : Exception
    {
        public long StatusCode { get; }
        public string Error { get; }
        public string Body { get; }

        public TokenEndpointException(long statusCode, string error, string body, Exception inner = null)
            : base($"Token endpoint error {statusCode}: {error}\n{body}", inner)
        {
            StatusCode = statusCode;
            Error = error;
            Body = body;
        }
    }

    public class RefreshTokenExpiredException : Exception
    {
        public RefreshTokenExpiredException(string message) : base(message) { }
        public RefreshTokenExpiredException(string message, Exception inner) : base(message, inner) { }
    }
}