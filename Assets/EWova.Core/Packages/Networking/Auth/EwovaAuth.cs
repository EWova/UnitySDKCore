using System.Collections.Generic;

namespace EWova.Auth
{
    public class EWovaAuth : AuthProvider
    {
        public readonly static EWovaAuth Instance = new();
        internal EWovaAuth()
            : base(EWovaAuthConfigFactory.Create(options =>
            {
                // TODO: 這邊的 client 暫時使用 learning-portfolio-sdk 待解偶
                options.ClientId = "learning-portfolio-sdk";
                options.Scopes = new List<string> { "openid", "profile", "email", "roles", "organization", "offline_access" };
            }), new Logger("[EWova]EWovaAuth ", LogLevel.Full))
        { }
    }
}
