using UnityEngine;

namespace EWova
{
    public static partial class EWova
    {
        public const string ApplicationScheme = "ewova";

        public static void LaunchApp()
            => Application.OpenURL(GetDeepLink());
        public static string GetDeepLink()
            => ApplicationScheme + "://";
    }
}
