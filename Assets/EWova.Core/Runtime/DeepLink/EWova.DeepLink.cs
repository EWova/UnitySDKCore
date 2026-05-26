using EWova.DeepLink;
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
    public static partial class EWova
    {
        public static void LaunchApp(DeepLinkQueryInclude include)
        {
            Application.OpenURL(GetDeepLink(include));
        }

        public static string GetDeepLink(DeepLinkQueryInclude include)
        {
            if(DeepLinkHandler.Default.Query.Count == 0)
            {
                return GetDeepLink();
            }

            var path = EWovaUriPath.Parse(DeepLinkScheme);

            if ((include & DeepLinkQueryInclude.LoginToken) != 0
                && DeepLinkHandler.Default.Query.TryGetValue(EWovaUriPath.Query.Token.Key, out var token))
            {
                path.AddOrSetQueue(EWovaUriPath.Query.Token, token);
            }

            if ((include & DeepLinkQueryInclude.WorldID) != 0
                    && DeepLinkHandler.Default.Query.TryGetValue(EWovaUriPath.Query.WorldID.Key, out var worldID))
            {
                path.AddOrSetQueue(EWovaUriPath.Query.WorldID, worldID);
            }

            if ((include & DeepLinkQueryInclude.SpaceID) != 0
                    && DeepLinkHandler.Default.Query.TryGetValue(EWovaUriPath.Query.SpaceID.Key, out var spaceID))
            {
                path.AddOrSetQueue(EWovaUriPath.Query.SpaceID, spaceID);
            }

            return path.GetResult().ToString();
        }
    }
}