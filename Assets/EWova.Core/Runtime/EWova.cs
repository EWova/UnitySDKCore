using EWova.NetService;

using UnityEngine;

namespace EWova
{
    [System.Flags] 
    public enum DeepLinkQueryInclude
    {
        None = 0,
        LoginToken = 1 << 0,
        WorldID = 1 << 1,
        SpaceID = 1 << 2,

        Default = LoginToken | WorldID | SpaceID
    }

    public static class EWova
    {
        public const string NetServiceApi = "https://dash.ewova.com/api/";
        public const string QueryPrefix = "ewova";

        private const string DeepLinkScheme = "ewova://";

        public static void LaunchApp() 
            => LaunchApp(DeepLinkQueryInclude.Default);
        public static void LaunchApp(DeepLinkQueryInclude include)
        {
            Application.OpenURL(GetDeepLink(include));
        }

        public static string GetDeepLink()
            => GetDeepLink(DeepLinkQueryInclude.Default);
        public static string GetDeepLink(DeepLinkQueryInclude include)
        {
            var path = EWovaUriPath.Parse(DeepLinkScheme);

            if ((include & DeepLinkQueryInclude.LoginToken) != 0)
            {
                path.AddOrSetQueue(EWovaUriPath.Query.Token);
            }

            if ((include & DeepLinkQueryInclude.WorldID) != 0)
            {
                path.AddOrSetQueue(EWovaUriPath.Query.WorldID);
            }

            if ((include & DeepLinkQueryInclude.SpaceID) != 0)
            {
                path.AddOrSetQueue(EWovaUriPath.Query.SpaceID);
            }

            return path.GetResult().ToString();
        }
    }
}
