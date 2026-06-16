using System;
using System.Collections.Generic;

using UnityEngine;

namespace EWova.DeepLink
{
    public class DeepLinkHandler
    {
        private readonly Dictionary<string, string> m_query = new();

        public static DeepLinkHandler Dummy = new DeepLinkHandler(null);
        public static DeepLinkHandler Default { get; private set; }
        public static bool IsSupported => s_defaultProvider != null;

        private static IDeepLinkProvider s_defaultProvider;

        public readonly string Scheme;

        public string ActiveURL { get; private set; } = string.Empty;
        public bool IsDummy => this == Dummy;
        public bool IsActivated => !string.IsNullOrEmpty(ActiveURL);
        public DateTime LastUpdated { get; private set; }

        public IReadOnlyDictionary<string, string> Query => m_query;

        private Action<DeepLinkHandler> m_onActivated;

        private const StringComparison SchemeCompare = StringComparison.OrdinalIgnoreCase;

        public static Logger Logger = new("[EWova]DeepLink ", LogLevel.Full);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Init()
        {
            s_defaultProvider = DeepLinkProviderDiscovery.Find();

            var config = DeepLinkConfig.LoadOrDefault();
            string err = null;
            if (config == null || !config.VerifyFormat(out err))
            {
                Logger.Err($"DeepLinkConfig invalid: {err}");
                Default = Dummy;
                return;
            }

            Default = Registry(config.MyAppScheme);
        }

        public static DeepLinkHandler Registry(string scheme)
        {
            if (string.IsNullOrEmpty(scheme))
                throw new ArgumentNullException(nameof(scheme));

            var handler = new DeepLinkHandler(scheme);

            return handler;
        }

        private DeepLinkHandler(string scheme)
        {
            Scheme = scheme;

            if (scheme == null)
                return;

            if (s_defaultProvider == null)
                return;

            s_defaultProvider.Initialize(scheme);
            s_defaultProvider.OnDeepLinkActivated += OnDeepLinkActivated;
        }

        public void ContinueWith(Action<DeepLinkHandler> action)
        {
            if (action == null)
                return;

            if (IsActivated)
                action(this);

            m_onActivated += action;
        }

        public void Remove(Action<DeepLinkHandler> action)
        {
            if (action == null)
                return;

            m_onActivated -= action;
        }

        private void OnDeepLinkActivated(string url)
        {
            if (string.IsNullOrEmpty(url))
                return;

            Uri uri;

            try
            {
                uri = new Uri(url);
            }
            catch
            {
                return;
            }

            if (!string.Equals(uri.Scheme, Scheme, SchemeCompare))
                return;

            ActiveURL = url;
            LastUpdated = DateTime.Now;

            m_query.Clear();

            var query = uri.Query;
            if (!string.IsNullOrEmpty(query))
            {
                var parsed = HttpUtility.ParseQueryString(query);

                foreach (string key in parsed.AllKeys)
                {
                    m_query[key ?? string.Empty] = parsed[key];
                }
            }

            m_onActivated?.Invoke(this);
        }
    }
}