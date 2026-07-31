using System.Collections.Generic;

namespace EWova.Auth
{
    public record EWovaAuthConfig
    {
        public string BaseAuthUrl => Issuer;
        public string AuthorizationEndpoint { get; }
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
            Issuer = baseAuthUrl.TrimEnd('/');

            AuthorizationEndpoint = "/oidc/authorization";
            TokenEndpoint = "/oidc/token";
            LaunchTicketEndpoint = "/oidc/launch-ticket";
            ExchangeTicketEndpoint = "/token";

            ClientId = clientId;
            RedirectUri = $"{customUriScheme}://callback";
            Scopes = string.Join(" ", scopes);
        }
    }
}
