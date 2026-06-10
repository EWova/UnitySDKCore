using System.Collections.Generic;

namespace EWova.Auth
{
    public static class EWovaAuthConfigFactory
    {
        public static EWovaAuthConfig Create(string appScheme, DeploymentMode env)
        {
            appScheme = appScheme?.Trim();
            if (string.IsNullOrEmpty(appScheme))
                throw new System.ArgumentNullException(nameof(appScheme));

            return env switch
            {
                DeploymentMode.Production => new EWovaAuthConfig(
                   clientId: "learning-portfolio-sdk",
                   baseAuthUrl: "https://auth.ewova.com",
                   customUriScheme: appScheme,
                   scopes: new List<string> { "openid", "profile", "email", "roles", "organization", "offline_access" }),

                DeploymentMode.Development => new EWovaAuthConfig(
                    clientId: "learning-portfolio-sdk",
                    baseAuthUrl: "https://auth.ewova.dev",
                    customUriScheme: appScheme,
                    scopes: new List<string> { "openid", "profile", "email", "roles", "organization", "offline_access" }),

                _ => throw new System.ArgumentException($"Unsupported environment: {env}")
            };
        }
    }
}
