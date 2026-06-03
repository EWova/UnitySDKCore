using Cysharp.Threading.Tasks;

using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

using System;
using System.Text;
using System.Threading;

using UnityEngine.Networking;
using UnityEngine.Scripting;

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

        public TokenService(EWovaAuthConfig config)
        {
            m_config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public async UniTask<TokenSet> ExchangeCodeAsync(
            string code,
            string codeVerifier,
            string nonce,
            CancellationToken cancellationToken = default)
        {
            var response = await SendAsync<TokenResponse>(
                url: m_config.TokenEndpoint,
                body: m_config.BuildExchangeCodeBody(code, codeVerifier),
                contentType: "application/x-www-form-urlencoded",
                cancellationToken: cancellationToken);

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
            var response = await SendAsync<TokenResponse>(
                url: m_config.TokenEndpoint,
                body: m_config.BuildExchangeLaunchTicketBody(launchTicket),
                contentType: "application/x-www-form-urlencoded",
                cancellationToken: cancellationToken);

            return TokenSet.FromResponse(response);
        }

        public async UniTask<LaunchTicketResponse> CreateLaunchTicketAsync(
            string accessToken,
            string appId,
            CancellationToken ct = default)
        {
            return await SendAsync<LaunchTicketResponse>(
                url: m_config.LaunchTicketEndpoint,
                body: m_config.BuildCreateLaunchTicketJson(appId),
                contentType: "application/json",
                accessToken: accessToken,
                cancellationToken: ct);
        }

        public async UniTask<TokenSet> RefreshTokenAsync(
            string refreshToken,
            CancellationToken cancellationToken = default)
        {
            var body = m_config.BuildRefreshTokenBody(refreshToken);
            const int maxRetries = 3;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    var response = await SendAsync<TokenResponse>(
                        url: m_config.TokenEndpoint,
                        body: body,
                        contentType: "application/x-www-form-urlencoded",
                        cancellationToken: cancellationToken);

                    return TokenSet.FromResponse(response);
                }
                catch (Exception ex)
                {
                    if (ex is RefreshTokenExpiredException) throw;
                    if (attempt == maxRetries - 1) throw;
                    await UniTask.Delay(TimeSpan.FromSeconds(1 << attempt), cancellationToken: cancellationToken);
                }
            }
            throw new TokenEndpointException(500, "refresh_failed", "Refresh token failed after retries");
        }

        private async UniTask<TResponse> SendAsync<TResponse>(
            string url,
            string body,
            string contentType,
            string accessToken = null,
            CancellationToken cancellationToken = default)
        {
            using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", contentType);

            if (!string.IsNullOrEmpty(accessToken))
                req.SetRequestHeader("Authorization", $"Bearer {accessToken}");

            await req.SendWebRequest().WithCancellation(cancellationToken);
            var text = req.downloadHandler?.text ?? string.Empty;

            if (req.result != UnityWebRequest.Result.Success)
                throw CreateTokenException(req.responseCode, text);

            try
            {
                return JsonConvert.DeserializeObject<TResponse>(text);
            }
            catch (Exception ex)
            {
                throw new TokenEndpointException(req.responseCode, "invalid_response", text, ex);
            }
        }

        private static string TryParseErrorField(string json)
        {
            try
            {
                var obj = JsonConvert.DeserializeObject<JObject>(json);
                return obj?["error"]?.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static Exception CreateTokenException(long statusCode, string body, Exception inner = null)
        {
            var error = TryParseErrorField(body);
            if (statusCode == 400 && string.Equals(error, "invalid_grant", StringComparison.OrdinalIgnoreCase))
            {
                return new RefreshTokenExpiredException("Refresh token has expired or is invalid. Re-authentication required.", new TokenEndpointException(statusCode, error, body, inner));
            }
            return new TokenEndpointException(statusCode, error, body, inner);
        }

        [Preserve]
        private class IdTokenPayload
        {
            [JsonProperty("nonce")] public string Nonce { get; set; }
            [JsonProperty("iss")] public string Issuer { get; set; }
            [JsonProperty("aud")] public string Audience { get; set; }
            [JsonProperty("exp")] public long Expiry { get; set; }
            [JsonProperty("sub")] public string Subject { get; set; }
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