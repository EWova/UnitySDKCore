using System.Text;

using UnityEngine.Networking;

namespace EWova.Auth
{
    public record OidcConfig
    {
        public string AuthorizationUrl { get; }
        public string TokenEndpoint { get; }
        public string LaunchTicketEndpoint { get; }
        public string ClientId { get; }
        public string RedirectUri { get; }
        public string Scopes { get; }
        public string Issuer { get; }
        public string ExchangeTicketEndpoint { get; }

        internal OidcConfig(
            string clientId,
            string baseAuthUrl,
            string redirectUri,
            string scope)
        {
            AuthorizationUrl = $"{baseAuthUrl}/oidc/authorization";
            TokenEndpoint = $"{baseAuthUrl}/oidc/token";
            LaunchTicketEndpoint = $"{baseAuthUrl}/oidc/launch-ticket";
            ClientId = clientId;
            RedirectUri = redirectUri;
            Scopes = scope;
            Issuer = baseAuthUrl.TrimEnd('/');
            ExchangeTicketEndpoint = $"{baseAuthUrl}/token";
        }

        /// <summary>
        ///     組建 /authorize URL（含 PKCE 與 OIDC 必要參數）
        /// </summary>
        public string BuildAuthorizeUrl(string codeChallenge, string state, string nonce, string uiLocales = null)
        {
            var sb = new StringBuilder(AuthorizationUrl);
            sb.Append("?response_type=code");
            sb.Append($"&client_id={UnityWebRequest.EscapeURL(ClientId)}");
            sb.Append($"&redirect_uri={UnityWebRequest.EscapeURL(RedirectUri)}");
            sb.Append($"&scope={UnityWebRequest.EscapeURL(Scopes)}");
            sb.Append($"&state={state}");
            sb.Append($"&nonce={nonce}");
            sb.Append($"&code_challenge={codeChallenge}");
            sb.Append("&code_challenge_method=S256");
            if (!string.IsNullOrEmpty(uiLocales))
                sb.Append($"&ui_locales={UnityWebRequest.EscapeURL(uiLocales)}");
            return sb.ToString();
        }
    }
}
