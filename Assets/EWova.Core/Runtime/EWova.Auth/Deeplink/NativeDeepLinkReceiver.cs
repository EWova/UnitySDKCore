using System;

using UnityEngine;

namespace EWova.Auth
{
    /// <summary>
    /// 使用 Unity 原生的 DeepLink 功能接收 URL，適用於 Android/iOS 平台
    /// </summary>
    public class NativeDeepLinkReceiver : IDeepLinkReceiver
    {
        public string Name => "Native";

        private Action<IDeepLinkReceiver, string> _callback;

        public void Initialize(Action<IDeepLinkReceiver, string> onUrlReceived)
        {
            _callback = onUrlReceived;
            Application.deepLinkActivated += OnActivated;
            if (!string.IsNullOrEmpty(Application.absoluteURL))
                OnActivated(Application.absoluteURL);
        }

        private void OnActivated(string url) => _callback?.Invoke(this, url);

        void IDisposable.Dispose() => Application.deepLinkActivated -= OnActivated;
    }
}
