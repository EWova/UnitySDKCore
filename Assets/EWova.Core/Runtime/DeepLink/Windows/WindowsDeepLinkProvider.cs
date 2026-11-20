using System;

namespace EWova.DeepLink.Win
{
    [DeepLinkProvider(typeof(WindowsDeepLinkProvider))]
    public sealed class WindowsDeepLinkProvider : IDeepLinkProvider
    {
        public int Priority => 100;

#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
        public bool IsSupported => true;
        public event Action<string, DeepLinkInvocationType> OnDeepLinkActivated;
        private string _scheme = null;
        public bool ConfigureScheme(string scheme, out string errorMessage)
        {
            if (_scheme != null)
            {
                errorMessage = $"Windows DeepLink 已經配置過 Scheme: {_scheme}，無法再次配置新的 Scheme: {scheme}";
                return false;
            }

            _scheme = scheme;
            WindowsDeepLinking.OnDeepLinkActivated += OnActivated;
            WindowsDeepLinking.Initialize(scheme);
            errorMessage = null;
            return true;
        }

        private void OnActivated(string url, DeepLinkInvocationType type)
        {
            OnDeepLinkActivated?.Invoke(url, type);
        }
#else
        public bool IsSupported => false;
#pragma warning disable CS0067
        public event Action<string, DeepLinkInvocationType> OnDeepLinkActivated;
#pragma warning restore CS0067
        public bool ConfigureScheme(string scheme, out string errorMessage)
        {
            errorMessage = $"Windows DeepLink 在此平台不受支援，無法配置 Scheme: {scheme}";
            return false;
        }
#endif
    }
}