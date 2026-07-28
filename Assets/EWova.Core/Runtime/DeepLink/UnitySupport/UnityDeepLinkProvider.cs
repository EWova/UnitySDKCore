using System;

using UnityEngine;
using UnityEngine.Scripting;
namespace EWova.DeepLink
{
    // 僅透過 DeepLinkProviderDiscovery 以 Activator.CreateInstance 反射建立，
    // 沒有任何程式碼直接 new 此類別，IL2CPP 在 stripping 時可能會移除建構子與成員，故需 [Preserve]。
    [Preserve]
    [DeepLinkProvider(typeof(UnityDeepLinkProvider))]
    public class UnityDeepLinkProvider : IDeepLinkProvider
    {
        public int Priority => 0;

        public bool IsSupported
        {
            get
            {
#if (UNITY_ANDROID || UNITY_IOS || UNITY_WSA|| UNITY_STANDALONE_OSX) && !UNITY_EDITOR_WIN
                return true;
#else
                return false;
#endif
            }
        }

        public event Action<string, DeepLinkInvocationType> OnDeepLinkActivated;

        public bool ConfigureScheme(string scheme, out string errorMessage)
        {
            Application.deepLinkActivated += OnActivated;
            errorMessage = null;
            return true;
        }

        private void OnActivated(string url)
        {
            OnDeepLinkActivated?.Invoke(url, DeepLinkInvocationType.Runtime);
        }
    }
}
