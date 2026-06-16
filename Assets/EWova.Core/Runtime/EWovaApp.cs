using EWova.DeepLink;

using System;
using System.Collections.Generic;

using UnityEngine;

namespace EWova
{
    [Flags]
    public enum LaunchViaDeepLinkOption
    {
        JustLaunch = 0,

        BackToWorld = 1 << 0,

        BackToWorldAndSpace = BackToWorld | 1 << 1,

        Default = BackToWorldAndSpace
    }

    public static class EWovaApp
    {
        public const string Scheme = "ewova";

        private static bool _subscribed;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (_subscribed)
                return;
            _subscribed = true;
            DeepLinkHandler.Default.ContinueWith(LoadFromDeepLink);
        }

        /// <summary>
        /// 如果有值，代表是從 EWova 元宇宙應用程式啟動到或跳轉到此應用程式，可以從中取得相關的課程世界或空間資訊
        /// </summary>
        public static EWovaAppLaunchContext LaunchContext { get; internal set; } = null;
        /// <summary>
        /// EWova 元宇宙應用程式的 Deep Link URL
        /// </summary>
        public static string AppDeepLink = $"{Scheme}://";

        public static string GetDeepLink(
            LaunchViaDeepLinkOption option,
            IReadOnlyDictionary<string, string> extraQuery = null)
        {
            var builder = new UriBuilder
            {
                Scheme = Scheme,
                Host = string.Empty
            };

            Dictionary<string, string> queryDict = extraQuery == null ? new() : new(extraQuery);

            if (option == LaunchViaDeepLinkOption.BackToWorld ||
                option == LaunchViaDeepLinkOption.BackToWorldAndSpace)
            {
                if (LaunchContext?.WorldGuid is Guid worldGuid)
                {
                    queryDict[EWovaAppLaunchContext.WorldIdKey] =
                        worldGuid.ToString();
                    if (option == LaunchViaDeepLinkOption.BackToWorldAndSpace &&
                        LaunchContext.SpaceInstanceIndex is int spaceId)
                    {
                        queryDict[EWovaAppLaunchContext.SpaceIdKey] =
                            spaceId.ToString();
                    }
                }
            }

            if (queryDict.Count > 0)
            {
                var query = HttpUtility.ParseQueryString(string.Empty);
                foreach (var kv in queryDict)
                {
                    query[kv.Key] = kv.Value;
                }
                builder.Query = query.ToString();
            }

            return builder.ToString();
        }

        public static void LaunchViaDeepLink(
            LaunchViaDeepLinkOption option = LaunchViaDeepLinkOption.Default,
            IReadOnlyDictionary<string, string> extraQuery = null)
        {
            if (option == LaunchViaDeepLinkOption.Default)
            {
                if (LaunchContext != null)
                {
                    option = LaunchViaDeepLinkOption.BackToWorldAndSpace;
                }
                else
                {
                    option = LaunchViaDeepLinkOption.JustLaunch;
                }
            }

            Application.OpenURL(GetDeepLink(option, extraQuery));
        }

        private static void LoadFromDeepLink(DeepLinkHandler handler)
        {
            var url = handler.Query;
            string wid = url.ContainsKey(EWovaAppLaunchContext.WorldIdKey) ? url[EWovaAppLaunchContext.WorldIdKey] : null;
            string sid = url.ContainsKey(EWovaAppLaunchContext.SpaceIdKey) ? url[EWovaAppLaunchContext.SpaceIdKey] : null;
            if (wid != null || sid != null)
            {
                var context = new EWovaAppLaunchContext();
                if (wid != null && Guid.TryParse(wid, out var worldGuid))
                {
                    context.WorldGuid = worldGuid;
                }
                if (sid != null && int.TryParse(sid, out var spaceInstanceIndex))
                {
                    context.SpaceInstanceIndex = spaceInstanceIndex;
                }

                Logger.Default.Info("透過 EWova App 使用 DeepLink 穿越來，LaunchContext: " + $"WorldGuid={context.WorldGuid}, SpaceInstanceIndex={context.SpaceInstanceIndex}");
                LaunchContext = context;
            }
        }
    }
}
