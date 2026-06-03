using Newtonsoft.Json;

using System.Collections.Generic;
using System.Linq;

using UnityEngine.Networking;

namespace EWova.Auth
{
    public record EWovaAuthConfig
    {
        public string AuthorizationUrl { get; }
        public string TokenEndpoint { get; }
        public string LaunchTicketEndpoint { get; }
        public string ClientId { get; }
        public string RedirectUri { get; }
        public string Scopes { get; }
        public string Issuer { get; }
        public string ExchangeTicketEndpoint { get; }

        internal EWovaAuthConfig(
            string clientId,
            string baseAuthUrl,
            string customUriScheme,
            IEnumerable<string> scopes)
        {
            AuthorizationUrl = $"{baseAuthUrl}/oidc/authorization";
            TokenEndpoint = $"{baseAuthUrl}/oidc/token";
            LaunchTicketEndpoint = $"{baseAuthUrl}/oidc/launch-ticket";
            ClientId = clientId;
            RedirectUri = $"{customUriScheme}://callback";
            Scopes = string.Join(" ", scopes);
            Issuer = baseAuthUrl.TrimEnd('/');
            ExchangeTicketEndpoint = $"{baseAuthUrl}/token";
        }

        /// <summary>
        ///     組建 /authorize URL（含 PKCE 與 OIDC 必要參數）
        /// </summary>
        public string BuildAuthorizeUrl(
            string codeChallenge,
            string state,
            string nonce,
            string uiLocales = null)
        {
            var query = BuildEncodedParameters(new Dictionary<string, string>
            {
                ["response_type"] = "code",
                ["client_id"] = ClientId,
                ["redirect_uri"] = RedirectUri,
                ["scope"] = Scopes,
                ["state"] = state,
                ["nonce"] = nonce,
                ["code_challenge"] = codeChallenge,
                ["code_challenge_method"] = "S256",
                ["ui_locales"] = uiLocales,
            });

            return $"{AuthorizationUrl}?{query}";
        }

        public string BuildExchangeCodeBody(string code, string codeVerifier)
        {
            return BuildEncodedParameters(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = RedirectUri,
                ["client_id"] = ClientId,
                ["code_verifier"] = codeVerifier,
            });
        }
        public string BuildExchangeLaunchTicketBody(string launchTicket)
        {
            return BuildEncodedParameters(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ewova:params:oauth:grant-type:launch-ticket",
                ["client_id"] = ClientId,
                ["launch_ticket"] = launchTicket,
            });
        }
        public string BuildRefreshTokenBody(string refreshToken)
        {
            return BuildEncodedParameters(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = ClientId,
                ["refresh_token"] = refreshToken,
                ["scope"] = Scopes,
            });
        }
        public string BuildCreateLaunchTicketJson(string appId)
        {
            return JsonConvert.SerializeObject(new { appId });
        }

        private static string BuildEncodedParameters(
            IEnumerable<KeyValuePair<string, string>> parameters)
        {
            return string.Join(
                "&",
                parameters
                    .Where(x => x.Value != null)
                    .Select(x =>
                        $"{x.Key}={UnityWebRequest.EscapeURL(x.Value)}"));
        }
    }
}
