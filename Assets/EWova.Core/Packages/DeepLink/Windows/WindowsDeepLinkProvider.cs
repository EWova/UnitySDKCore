using System;

namespace EWova.DeepLink.Win
{
    [DeepLinkProvider(typeof(WindowsDeepLinkProvider))]
    public sealed class WindowsDeepLinkProvider : IDeepLinkProvider
    {
        public int Priority => 100;

        public bool IsSupported =>
#if (UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN) && !(NET_STANDARD_2_0 || NET_STANDARD_2_1)
            true;
#else
            false;
#endif

        public event Action<string> OnDeepLinkActivated;

        public void Initialize(string scheme)
        {
            WindowsDeepLinking.Initialize(scheme);
            WindowsDeepLinking.OnDeepLinkActivated += OnActivated;
        }

        private void OnActivated(string url)
        {
            OnDeepLinkActivated?.Invoke(url);
        }
    }
}
