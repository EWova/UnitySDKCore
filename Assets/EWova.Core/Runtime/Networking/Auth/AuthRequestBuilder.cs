using Newtonsoft.Json;

using System.Collections.Generic;
using System;
using System.Linq;

namespace EWova.Auth
{
    public static class AuthRequestBuilder
    {
        /// <summary>
        ///     組建 /authorize URL（含 PKCE 與 OIDC 必要參數）
        /// </summary>
        public static string BuildAuthorizeUrl(
            EWovaAuthConfig config,
            string codeChallenge,
            string state,
            string nonce,
            AutoFill autoFill = default,
            string prompt = "login",
            string uiLocales = null)
        {
            var parameters = new Dictionary<string, string>
            {
                ["response_type"] = "code",
                ["client_id"] = EscapeDataStringOrNull(config.ClientId),
                ["redirect_uri"] = EscapeDataStringOrNull(config.RedirectUri),
                ["scope"] = EscapeDataStringOrNull(config.Scopes),
                ["state"] = state,
                ["nonce"] = nonce,
                ["code_challenge"] = codeChallenge,
                ["code_challenge_method"] = "S256",
                ["ui_locales"] = EscapeDataStringOrNull(uiLocales),
                ["prompt"] = EscapeDataStringOrNull(prompt)
            };

            if (autoFill.Method != null)
                parameters["login_method"] = EscapeDataStringOrNull(autoFill.Method);
            if (autoFill.Email != null)
                parameters["login_hint"] = EscapeDataStringOrNull(autoFill.Email);
            if (autoFill.QuickCode != null)
                parameters["quick_login_org"] = EscapeDataStringOrNull(autoFill.QuickCode);
            if (autoFill.QuickName != null)
                parameters["quick_login_hint"] = EscapeDataStringOrNull(autoFill.QuickName);

            var query = BuildQueryString(parameters);

            return $"{config.BaseAuthUrl}{config.AuthorizationEndpoint}?{query}";
        }

        public static string BuildExchangeCodeBody(
            EWovaAuthConfig config,
            string code,
            string codeVerifier)
        {
            return BuildQueryString(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = EscapeDataStringOrNull(config.RedirectUri),
                ["client_id"] = EscapeDataStringOrNull(config.ClientId),
                ["code_verifier"] = codeVerifier,
            });
        }
        public static string BuildExchangeLaunchTicketBody(
            EWovaAuthConfig config,
            string launchTicket)
        {
            return BuildQueryString(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ewova:params:oauth:grant-type:launch-ticket",
                ["client_id"] = EscapeDataStringOrNull(config.ClientId),
                ["launch_ticket"] = launchTicket,
            });
        }
        public static string BuildRefreshTokenBody(
            EWovaAuthConfig config,
            string refreshToken)
        {
            return BuildQueryString(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = EscapeDataStringOrNull(config.ClientId),
                ["refresh_token"] = refreshToken,
                ["scope"] = EscapeDataStringOrNull(config.Scopes),
            });
        }
        public static string BuildCreateLaunchTicketJsonBody(
            string appId)
        {
            return JsonConvert.SerializeObject(new { appId });
        }

        private static string EscapeDataStringOrNull(string value)
        {
            return value == null ? null : Uri.EscapeDataString(value);
        }
        private static string BuildQueryString(
            IEnumerable<KeyValuePair<string, string>> parameters)
        {
            return string.Join(
                "&",
                parameters
                    .Where(x => x.Value != null)
                    .Select(x => $"{x.Key}={x.Value}"));
        }
    }
}
