using UnityEngine;

namespace EWova
{
    public static partial class EWova
    {
        public const string ApiBaseUrl = "https://api-world.ewova.dev/api/v1";
        public const string QueryPrefix = "ewova";
        public const string DeepLinkScheme = "ewova://";

        public static void LaunchApp()
            => Application.OpenURL(GetDeepLink());
        public static string GetDeepLink()
            => DeepLinkScheme;
    }
}
