using System;
using System.Collections.Generic;

using UnityEngine;

namespace EWova.DeepLink
{
    /// <summary>
    /// DeepLink 的啟動方式
    /// </summary>
    public enum DeepLinkInvocationType
    {
        /// <summary>
        /// 啟動遊戲前就已存在的 DeepLink 資料
        /// </summary>
        Launch,
        /// <summary>
        /// 遊戲執行期間收到的 DeepLink
        /// </summary>
        Runtime
    }

    public class DeepLinkHandler : IDisposable
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void RuntimeInitialize()
        {
            s_defaultProvider = DeepLinkProviderDiscovery.Find();

            var config = DeepLinkConfig.LoadOrDefault();

            if (config == null)
            {
                if (Logger.ErrorEnabled)
                    Logger.Err($"此專案沒有預設的 DeepLink 設定，Default DeepLinkHandler 將不會被建立");
                return;
            }
            if (!config.VerifyFormat(out var err))
            {
                if (Logger.ErrorEnabled)
                    Logger.Err($"DeepLink 設定檔格式錯誤，Default DeepLinkHandler 將不會被建立，錯誤訊息: {err}");
                return;
            }

            s_default = Registry(config.MyAppScheme);

#if UNITY_EDITOR
            Authoring.EditorDomainReleaseHelper.CleanupOneShot += () =>
            {
                if (s_default != null)
                    ((IDisposable)s_default).Dispose();
                s_default = null;
                s_defaultProvider = null;
            };
#endif
        }

        private bool m_disposed;
        void IDisposable.Dispose()
        {
            if (m_disposed)
                return;

            m_disposed = true;

            if (Scheme != null && s_defaultProvider != null)
                s_defaultProvider.OnDeepLinkActivated -= OnDeepLinkActivated;

            m_onActivated = null;
            m_query.Clear();

            if (s_default == this)
                s_default = null;

            GC.SuppressFinalize(this);
        }
        private void VerifyNotDisposed()
        {
            if (m_disposed)
                throw new ObjectDisposedException(nameof(DeepLinkHandler));
        }

        private readonly Dictionary<string, string> m_query = new();
        private class EventDisposer : IDisposable
        {
            private readonly Action m_dispose;
            public EventDisposer(Action dispose)
            {
                m_dispose = dispose;
            }
            public void Dispose()
            {
                m_dispose?.Invoke();
            }
        }

        public readonly static DeepLinkHandler Dummy = new(null);
        public static DeepLinkHandler Default => s_default ?? Dummy;
        public static bool IsSupported => s_defaultProvider != null;
        private static IDeepLinkProvider s_defaultProvider;

        public readonly string Scheme;

        public string ActiveURL { get; private set; } = string.Empty;
        public DeepLinkInvocationType ActiveInvocationType { get; private set; } = DeepLinkInvocationType.Launch;
        public bool IsDummy => this == Dummy;
        public bool IsActivated => !string.IsNullOrEmpty(ActiveURL);
        public DateTime LastUpdated { get; private set; }

        public IReadOnlyDictionary<string, string> Query => m_query;

        private Action<DeepLinkHandler> m_onActivated;

        private static DeepLinkHandler s_default;
        private const StringComparison SchemeCompare = StringComparison.OrdinalIgnoreCase;

        public static Logger Logger = new("[EWova]DeepLink ", LogLevel.Full);
        public static DeepLinkHandler Registry(string scheme)
        {
            if (string.IsNullOrEmpty(scheme))
                throw new ArgumentNullException(nameof(scheme));

            var handler = new DeepLinkHandler(scheme);

            s_defaultProvider.OnDeepLinkActivated += handler.OnDeepLinkActivated;
            if (!s_defaultProvider.ConfigureScheme(scheme, out var errorMsg))
            {
                if (Logger.ErrorEnabled)
                    Logger.Err($"DeepLink 註冊失敗，Scheme: {scheme}，錯誤訊息: {errorMsg}");
                return null;
            }

            return handler;
        }

        private DeepLinkHandler(string scheme)
        {
            Scheme = scheme;
        }

        /// <summary>
        /// 當 DeepLink 被啟動時，會呼叫此方法註冊回呼函式，若 DeepLink 已經啟動過，則會立即呼叫回呼函式
        /// </summary>
        /// <returns>回傳 IDisposable 物件，當 Dispose 時會取消註冊回呼函式</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public IDisposable ContinueWith(Action<DeepLinkHandler> action)
        {
            VerifyNotDisposed();

            if (action == null)
                throw new ArgumentNullException(nameof(action));

            if (IsActivated)
                action(this);

            m_onActivated += action;
            return new EventDisposer(() => m_onActivated -= action);
        }

        private void OnDeepLinkActivated(string url, DeepLinkInvocationType type)
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
            ActiveInvocationType = type;
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