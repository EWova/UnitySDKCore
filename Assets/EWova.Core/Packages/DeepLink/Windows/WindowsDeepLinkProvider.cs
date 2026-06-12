using System;

namespace EWova.DeepLink.Win
{
    [DeepLinkProvider(typeof(WindowsDeepLinkProvider))]
    public sealed class WindowsDeepLinkProvider : IDeepLinkProvider
    {
        public int Priority => 100;

#if (UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN) && !(NET_STANDARD_2_0 || NET_STANDARD_2_1)
        public bool IsSupported => true;
           
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
#else
        public bool IsSupported => false;
        public event Action<string> OnDeepLinkActivated;
        public void Initialize(string scheme) { }
#endif

    }
}
