using EWova.DeepLink;

using System;
using System.Collections.Generic;

using UnityEngine;

namespace EWova
{
    public enum LaunchViaDeepLinkOption
    {
        Default = 0,
        JustLaunch = 1,
        BackToWorld = 2,
        BackToWorldAndSpace = 3,
    }

    public static class EWovaApp
    {
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
        public static string AppDeepLink = $"{EWova.ApplicationScheme}://";

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

            var builder = new UriBuilder
            {
                Scheme = EWova.ApplicationScheme,
                Host = ""
            };

            if (option != LaunchViaDeepLinkOption.JustLaunch)
            {
                var query = HttpUtility.ParseQueryString(builder.Query);

                switch (option)
                {
                    case LaunchViaDeepLinkOption.BackToWorld:
                        if (LaunchContext != null && LaunchContext.WorldGuid.HasValue)
                        {
                            query[EWovaAppLaunchContext.WorldIdKey] = LaunchContext.WorldGuid.Value.ToString();
                        }
                        else
                        {
                            if (Logger.Default.InfoEnabled)
                                Logger.Default.Info("你沒有從 EWova 元宇宙應用程式啟動到或跳轉到此應用程式，將會單純啟動此應用程式而不會回到任何課程世界");
                        }
                        break;
                    case LaunchViaDeepLinkOption.BackToWorldAndSpace:
                        if (LaunchContext != null && LaunchContext.WorldGuid.HasValue)
                        {
                            query[EWovaAppLaunchContext.WorldIdKey] = LaunchContext.WorldGuid.Value.ToString();

                            if (LaunchContext.SpaceInstanceIndex.HasValue)
                            {
                                query[EWovaAppLaunchContext.SpaceIdKey] = LaunchContext.SpaceInstanceIndex.Value.ToString();
                            }
                        }
                        else
                        {
                            if (Logger.Default.InfoEnabled)
                                Logger.Default.Info("你沒有從 EWova 元宇宙應用程式啟動到或跳轉到此應用程式，將會單純啟動此應用程式而不會回到任何課程世界或空間");
                        }
                        break;
                }

                builder.Query = query.ToString();
            }

            Application.OpenURL(builder.ToString());
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

                Logger.Default.Info("從 Deep Link 啟動，LaunchContext: " + $"WorldGuid={context.WorldGuid}, SpaceInstanceIndex={context.SpaceInstanceIndex}");
                LaunchContext = context;
            }
        }
    }
}
