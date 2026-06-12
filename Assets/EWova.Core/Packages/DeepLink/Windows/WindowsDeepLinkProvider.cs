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
#pragma warning disable CS0067
        public event Action<string> OnDeepLinkActivated;
#pragma warning restore CS0067
        public void Initialize(string scheme)
        {
#if UNITY_EDITOR_WIN && (NET_STANDARD_2_0 || NET_STANDARD_2_1)
            if (Authoring.DevelopTip.IsEnabled)
            {
                Authoring.EditorLogger.Warning("Windows DeepLink 在編輯器的 .NET Standard 2.x 模式下不受支援，請到 ProjectSettings/Player/OtherSettings/Configuration/ApiCompatibilityLevel 切換到 .NET Framework 以啟用此功能");
            }
#endif
        }
#endif
    }
}