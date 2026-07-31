using Cysharp.Threading.Tasks;

using EWova.Auth;
using EWova.DeepLink;

using System;
using System.Collections.Generic;
using System.Threading;

using UnityEngine;

namespace EWova
{
    [Flags]
    public enum EWovaDeepLinkLaunchOption
    {
        JustLaunch = 0,

        BackToWorld = 1 << 0,

        BackToWorldAndSpaceInstance = BackToWorld | 1 << 1,

        Default = BackToWorldAndSpaceInstance
    }

    public static class EWovaApp
    {
        public const string DeepLinkScheme = "ewova";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            var disposer = DeepLinkHandler.Default.ContinueWith(LoadFromDeepLink);
#if UNITY_EDITOR
            Authoring.EditorDomainReleaseHelper.CleanupOneShot += () =>
            {
                disposer.Dispose();
                InvocationContext = null;
            };
#endif
        }

        /// <summary>
        /// 如果有值，代表此 EWova App 是由其他應用程式透過 EWova Deep Link 啟動，
        /// 可從中取得啟動時附帶的世界或空間資訊。
        /// </summary>
        public static EWovaAppInvocationContext InvocationContext { get; internal set; } = null;
        /// <summary>
        /// EWova 元宇宙應用程式的 Deep Link URL
        /// </summary>
        public static string DeepLinkPrefix = $"{DeepLinkScheme}://";

        // TODO: 邏輯可能有問題
        /// <summary>
        /// 客戶端提供的 Project AppId，若要使用 Deep Link 取得 Launch Ticket，請先設定此值。
        /// </summary>
        public static string ClientProjectAppId
        {
            set => _clientProjectAppId = value;
        }
        private static string _clientProjectAppId = null;


        /// <summary>
        /// 取得 Deep Link URL，並可選擇附帶世界或空間資訊。 並附帶 Launch Ticket 讓 EWova App 可延續登入狀態。(需先設定  EWovaApp.AppId )
        /// </summary>
        public static async UniTask<string> GetDeepLink(
            EWovaDeepLinkLaunchOption option,
            IReadOnlyDictionary<string, string> extraQuery = null,
            CancellationToken ct = default)
        {
            var builder = new UriBuilder
            {
                Scheme = DeepLinkScheme,
                Host = string.Empty
            };

            var queryDict = BuildContextQuery(option);

            if (!string.IsNullOrEmpty(_clientProjectAppId))
            {
                try
                {
                    string launchTicket = await EWovaAuth.Instance.CreateLaunchTicketAsync(_clientProjectAppId, ct);
                    if (!string.IsNullOrEmpty(launchTicket))
                        queryDict[AuthProvider.LaunchTicketQueryKey] = launchTicket;
                }
                catch (Exception ex)
                {
                    Logger.Default.Warn($"取得 launch ticket 失敗，將以未登入狀態組出 Deep Link: {ex.Message}");
                }
            }

            if (extraQuery != null)
            {
                foreach (var kv in extraQuery)
                {
                    queryDict[kv.Key] = kv.Value;
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

        private static Dictionary<string, string> BuildContextQuery(EWovaDeepLinkLaunchOption option)
        {
            var queryDict = new Dictionary<string, string>();

            bool backToWorld =
                (option & EWovaDeepLinkLaunchOption.BackToWorld) != 0;

            bool backToWorldAndSpace =
                (option & EWovaDeepLinkLaunchOption.BackToWorldAndSpaceInstance) ==
                EWovaDeepLinkLaunchOption.BackToWorldAndSpaceInstance;

            if (backToWorld &&
                InvocationContext?.WorldGuid is Guid worldGuid)
            {
                queryDict[EWovaAppInvocationContext.WorldIdKey] =
                    worldGuid.ToString();

                if (backToWorldAndSpace &&
                    InvocationContext.SpaceInstanceIndex is int spaceId)
                {
                    queryDict[EWovaAppInvocationContext.SpaceIdKey] =
                        spaceId.ToString();
                }
            }

            return queryDict;
        }

        public static void LaunchViaDeepLink(
            EWovaDeepLinkLaunchOption option = EWovaDeepLinkLaunchOption.Default,
            IReadOnlyDictionary<string, string> extraQuery = null)
        {
            LaunchViaDeepLinkAsync(option, extraQuery).Forget();
        }

        private static async UniTaskVoid LaunchViaDeepLinkAsync(
            EWovaDeepLinkLaunchOption option,
            IReadOnlyDictionary<string, string> extraQuery)
        {
            if (option == EWovaDeepLinkLaunchOption.Default)
            {
                if (InvocationContext != null)
                {
                    option = EWovaDeepLinkLaunchOption.BackToWorldAndSpaceInstance;
                }
                else
                {
                    option = EWovaDeepLinkLaunchOption.JustLaunch;
                }
            }
            string deepLink = await GetDeepLink(option, extraQuery);
            Application.OpenURL(deepLink);
        }

        private static void LoadFromDeepLink(DeepLinkHandler handler)
        {
            var url = handler.Query;

            string wid = url.ContainsKey(EWovaAppInvocationContext.WorldIdKey) ? url[EWovaAppInvocationContext.WorldIdKey] : null;
            string sid = url.ContainsKey(EWovaAppInvocationContext.SpaceIdKey) ? url[EWovaAppInvocationContext.SpaceIdKey] : null;
            if (wid != null || sid != null)
            {
                var context = new EWovaAppInvocationContext();
                if (wid != null && Guid.TryParse(wid, out var worldGuid))
                {
                    context.WorldGuid = worldGuid;
                }
                if (sid != null && int.TryParse(sid, out var spaceInstanceIndex))
                {
                    context.SpaceInstanceIndex = spaceInstanceIndex;
                }

                Logger.Default.Info(
                    "透過 EWova 啟動 DeepLink 過來的，InvocationContext: " +
                    $"WorldGuid={context.WorldGuid}, SpaceInstanceIndex={context.SpaceInstanceIndex}"
                );
                InvocationContext = context;
                return;
            }

            string source = url.ContainsKey("source") ? url["source"] : null;
            if (string.Equals(source, "ewovaapp", StringComparison.OrdinalIgnoreCase))
            {
                Logger.Default.Info(
                    "透過 EWova 啟動 DeepLink 過來的，沒有附帶世界或空間資訊"
                );
                InvocationContext = new EWovaAppInvocationContext();
                return;
            }
        }
    }
}
