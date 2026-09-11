using EWova.DeepLink;

using System.Collections.Generic;

using UnityEngine;

namespace EWova.Auth
{
    public class EWovaAuth : AuthProvider
    {
        public static EWovaAuth Instance
        {
            get
            {
                _instance ??= new EWovaAuth();
                return _instance;
            }
        }
        private static EWovaAuth _instance;

        internal EWovaAuth()
            : base(EWovaAuthConfigFactory.Create(options =>
            {
                // TODO: 這邊的 client 暫時使用 learning-portfolio-sdk 待解偶
                options.ClientId = "learning-portfolio-sdk";
                options.Scopes = new List<string> { "openid", "profile", "email", "roles", "organization", "offline_access" };
            }), deepLinkHandler: DeepLinkHandler.Default
            , logger: new Logger("[EWova]EWovaAuth ", LogLevel.Full))
        { }
    }
}
