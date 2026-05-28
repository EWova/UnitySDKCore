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

    /// <summary>
    ///     負責所有與 Authorization Server 的 Token 相關 HTTP 通訊：
    ///     - Authorization Code → Token 交換
    ///     - Refresh Token 交換
    ///     - id_token 本地 JWT Payload 驗證
    /// </summary>
    public class TokenService
    {
        private readonly OidcConfig m_config;

        public TokenService(OidcConfig config)
        {
            m_config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        ///     以 Authorization Code 換取 TokenSet（適用於標準網頁登入成功後，從 Deep Link 拿到的 code）。
        /// </summary>
        public async UniTask<TokenSet> ExchangeCodeAsync(
            string code,
            CancellationToken cancellationToken = default)
        {
            var formBody = $"grant_type=authorization_code" +
                           $"&code={Uri.EscapeDataString(code)}" +
                           $"&client_id={Uri.EscapeDataString(m_config.ClientId)}" +
                           $"&redirect_uri={Uri.EscapeDataString(m_config.RedirectUri)}";

            var response = await SendAsync<TokenResponse>(
                url: m_config.TokenEndpoint,
                body: formBody,
                contentType: "application/x-www-form-urlencoded",
                cancellationToken: cancellationToken);

            return TokenSet.FromResponse(response);
        }

        /// <summary>
        ///     以 Launch Ticket 換取 TokenSet（適用於從其他 App 啟動後拿到的 launch_ticket）。
        /// </summary>
        /// <exception cref="RefreshTokenGrantException">當 AS 回傳 HTTP 400 invalid_grant，表示 launch_ticket 無效或已過期</exception>
        public async UniTask<TokenSet> ExchangeLaunchTicketAsync(
            string launchTicket,
            CancellationToken cancellationToken = default)
        {
            var formBody = $"grant_type=urn:ewova:params:oauth:grant-type:launch-ticket" +
                           $"&launch_ticket={Uri.EscapeDataString(launchTicket)}" +
                           $"&client_id={Uri.EscapeDataString(m_config.ClientId)}";

            var response = await SendAsync<TokenResponse>(
                url: m_config.TokenEndpoint,
                body: formBody,
                contentType: "application/x-www-form-urlencoded",
                cancellationToken: cancellationToken);

            return TokenSet.FromResponse(response);
        }

        /// <summary>
        ///     向 AS 請求 launch_ticket（需攜帶有效的 access_token），供從其他 App 啟動後拿到 launch_ticket 後交換 TokenSet。
        /// </summary>
        public async UniTask<LaunchTicketResponse> CreateLaunchTicketAsync(
            string accessToken,
            string appId,
            CancellationToken ct = default)
        {
            var jsonBody = JsonConvert.SerializeObject(new { appId });

            return await SendAsync<LaunchTicketResponse>(
                url: m_config.LaunchTicketEndpoint,
                body: jsonBody,
                contentType: "application/json",
                accessToken: accessToken,
                cancellationToken: ct);
        }

        /// <summary>
        ///     使用有效的 Refresh Token 刷新並獲取全新的 TokenSet。
        /// </summary>
        /// <exception cref="RefreshTokenGrantException">當 AS 回傳 HTTP 400 invalid_grant，表示 refresh_token 已失效或已被重複使用</exception>
        public async UniTask<TokenSet> RefreshAsync(
            string refreshToken,
            CancellationToken cancellationToken = default)
        {
            var formBody = $"grant_type=refresh_token" +
                           $"&refresh_token={Uri.EscapeDataString(refreshToken)}" +
                           $"&client_id={Uri.EscapeDataString(m_config.ClientId)}";

            var response = await SendAsync<TokenResponse>(
                url: m_config.TokenEndpoint,
                body: formBody,
                contentType: "application/x-www-form-urlencoded",
                cancellationToken: cancellationToken);

            return TokenSet.FromResponse(response);
        }

        #region HTTP 工具
        /// <summary>
        ///     Unified HTTP POST engine supporting pluggable content types and authorization context.
        /// </summary>
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
            {
                req.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            }

            try
            {
                await req.SendWebRequest().WithCancellation(cancellationToken);

                if (req.result != UnityWebRequest.Result.Success)
                {
                    ThrowApiError(req);
                }

                return JsonConvert.DeserializeObject<TResponse>(req.downloadHandler.text);
            }
            catch (UnityWebRequestException ex)
            {
                ThrowApiError(ex.UnityWebRequest);
                throw;
            }
        }

        private static string TryParseErrorField(string json)
        {
            try
            {
                var obj = JsonConvert.DeserializeObject<JObject>(json);
                var token = obj?["error"];
                return token != null ? (string)token : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
        private static void ThrowApiError(UnityWebRequest req)
        {
            var errorBody = req.downloadHandler?.text ?? string.Empty;

            var error = TryParseErrorField(errorBody);

            throw error switch
            {
                "invalid_grant" => new RefreshTokenGrantException(new TokenEndpointException(req.responseCode, error, errorBody)),
                _ => new TokenEndpointException(req.responseCode, error, errorBody)
            };
        }
        #endregion

        #region DTO
        [Preserve]
        private class IdTokenPayload
        {
            [JsonProperty("nonce")] public string Nonce { get; set; }
            [JsonProperty("iss")] public string Issuer { get; set; }
            [JsonProperty("aud")] public string Audience { get; set; }
            [JsonProperty("exp")] public long Expiry { get; set; }
            [JsonProperty("sub")] public string Subject { get; set; }
        }
        #endregion
    }

    /// <summary>/token 端點回傳 HTTP 錯誤</summary>
    public class TokenEndpointException : Exception
    {
        public long StatusCode { get; }
        public string Error { get; }
        public string Body { get; }

        public TokenEndpointException(long statusCode, string error, string body)
            : base($"Token endpoint error {statusCode}: {error}\n{body}")
        {
            StatusCode = statusCode;
            Error = error;
            Body = body;
        }
    }

    /// <summary>Token 無效或已過期（invalid_grant）</summary>
    public class RefreshTokenGrantException : Exception
    {
        public RefreshTokenGrantException(Exception inner)
            : base("Refresh token has expired. Re-authentication required.", inner)
        {
        }
    }
}
