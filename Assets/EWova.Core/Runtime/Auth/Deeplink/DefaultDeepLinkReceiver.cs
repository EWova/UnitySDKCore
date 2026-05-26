using EWova.DeepLink;

using System;

namespace EWova.Auth
{
    /// <summary>
    /// 使用自訂的 DeepLinkHandler 接收 URL，適用於 Windows 平台
    /// </summary>
    public class DefaultDeepLinkReceiver : IDeepLinkReceiver
    {
        public string Name => "Default";

        private Action<IDeepLinkReceiver, string> _callback;

        public void Initialize(Action<IDeepLinkReceiver, string> onUrlReceived)
        {
            _callback = onUrlReceived;
            DeepLink.DeepLinkHandler.Default.ContinueWith(OnActivated);
        }

        private void OnActivated(DeepLinkHandler url)
        {
            _callback?.Invoke(this, url.ActiveURL);
        }

        public void Dispose()
        {
            DeepLink.DeepLinkHandler.Default.ContinueWith(null);
            _callback = null;
        }
    }
}
