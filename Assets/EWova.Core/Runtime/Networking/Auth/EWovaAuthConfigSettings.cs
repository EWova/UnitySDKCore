using EWova.DeepLink;

using System;
using System.Collections.Generic;

namespace EWova.Auth
{
    public static class EWovaAuthConfigFactory
    {
        public class Options
        {
            public string ClientId { get; set; }
            public List<string> Scopes { get; set; }
        }
        public static EWovaAuthConfig Create(Action<Options> options)
        {
            string appScheme = DeepLinkHandler.Default.Scheme;

            if (string.IsNullOrEmpty(appScheme))
                appScheme = "blank-app-scheme";

            var opts = new Options();
            options?.Invoke(opts);

            if (string.IsNullOrEmpty(opts.ClientId))
                throw new System.ArgumentException("ClientId must be provided in the options.");

            if (opts.Scopes == null || opts.Scopes.Count == 0)
                throw new System.ArgumentException("Scopes must be provided in the options.");

            var env = Environment.DeploymentMode;
            return env switch
            {
                DeploymentMode.Production => new EWovaAuthConfig(
                   clientId: opts.ClientId,
                   baseAuthUrl: "https://auth.ewova.com",
                   customUriScheme: appScheme,
                   scopes: opts.Scopes),

                DeploymentMode.Development => new EWovaAuthConfig(
                    clientId: opts.ClientId,
                    baseAuthUrl: "https://auth.ewova.dev",
                    customUriScheme: appScheme,
                    scopes: opts.Scopes),

                _ => throw new System.ArgumentException($"Unsupported environment: {env}")
            };
        }
    }
}
